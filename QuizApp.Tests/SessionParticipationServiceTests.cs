using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using QuizApp.Server.Application.QuizImport;
using QuizApp.Server.Application.Quizzes;
using QuizApp.Server.Application.Sessions;
using QuizApp.Server.Persistence;
using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Tests;

public class SessionParticipationServiceTests
{
    [Fact]
    public async Task JoinSessionAsync_ValidRequest_PublishesTeamJoinedEvent()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var realtimePublisher = new FakeSessionRealtimePublisher();
        var sessionService = CreateSessionService(dbContext, realtimePublisher);

        var session = await CreateWaitingSessionAsync(quizService, CancellationToken.None);

        var result = await sessionService.JoinSessionAsync(new JoinSessionRequest(session.JoinCode, "Tým Echo"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(realtimePublisher.Events, x => x.SessionId == session.SessionId && x.EventName == RealtimeEventName.TeamJoined);
    }

    [Fact]
    public async Task ProgressDueSessionsAsync_LastQuestionExpired_PublishesRealtimeFinishedEvents()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var realtimePublisher = new FakeSessionRealtimePublisher();
        var sessionService = CreateSessionService(dbContext, realtimePublisher);

        var created = await CreateWaitingSessionWithTwoQuestionsAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Sigma"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        session.SetCurrentQuestion(1, DateTime.UtcNow.AddSeconds(-20), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        realtimePublisher.Events.Clear();
        await sessionService.ProgressDueSessionsAsync(CancellationToken.None);

        Assert.Contains(realtimePublisher.Events, x => x.SessionId == created.SessionId && x.EventName == RealtimeEventName.SessionFinished);
        Assert.DoesNotContain(realtimePublisher.Events, x => x.SessionId == created.SessionId && x.EventName == RealtimeEventName.ResultsReady);
    }

    [Fact]
    public async Task JoinSessionAsync_ValidRequest_ReturnsTeamIdentityAndReconnectToken()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var session = await CreateWaitingSessionAsync(quizService, CancellationToken.None);

        var result = await sessionService.JoinSessionAsync(new JoinSessionRequest(session.JoinCode, "Tým Alfa"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(session.SessionId, result.Response!.SessionId);
        Assert.Equal(SessionStatus.Waiting, result.Response.Status);
        Assert.Equal(64, result.Response.TeamReconnectToken.Length);

        var storedTeam = await dbContext.Teams.SingleAsync(x => x.TeamId == result.Response.TeamId);
        Assert.Equal("Tým Alfa", storedTeam.Name);
        Assert.NotEqual(result.Response.TeamReconnectToken, storedTeam.TeamReconnectTokenHash);
    }

    [Fact]
    public async Task LeaveSessionAsync_ValidRequest_RemovesTeamFromSession()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var realtimePublisher = new FakeSessionRealtimePublisher();
        var sessionService = CreateSessionService(dbContext, realtimePublisher);

        var session = await CreateWaitingSessionAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(session.JoinCode, "Tým Odchod"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var leaveResult = await sessionService.LeaveSessionAsync(
            session.SessionId,
            joinResult.Response!.TeamId,
            joinResult.Response.TeamReconnectToken,
            CancellationToken.None);

        Assert.True(leaveResult.IsSuccess);
        Assert.DoesNotContain(await dbContext.Teams.ToListAsync(), x => x.TeamId == joinResult.Response.TeamId);
        Assert.Contains(realtimePublisher.Events, x => x.SessionId == session.SessionId && x.EventName == RealtimeEventName.TeamLeft);
    }

    [Fact]
    public async Task JoinSessionAsync_DuplicateTeamName_ReturnsConflict()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var session = await CreateWaitingSessionAsync(quizService, CancellationToken.None);

        var firstJoin = await sessionService.JoinSessionAsync(new JoinSessionRequest(session.JoinCode, "Tým Beta"), CancellationToken.None);
        Assert.True(firstJoin.IsSuccess);

        var secondJoin = await sessionService.JoinSessionAsync(new JoinSessionRequest(session.JoinCode, "tým beta"), CancellationToken.None);

        Assert.False(secondJoin.IsSuccess);
        Assert.NotNull(secondJoin.Error);
        Assert.Equal(ApiErrorCode.TeamNameAlreadyUsed, secondJoin.Error!.Code);
    }

    [Fact]
    public async Task JoinSessionAsync_TeamNameIsSanitizedBeforePersist()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var session = await CreateWaitingSessionAsync(quizService, CancellationToken.None);
        var result = await sessionService.JoinSessionAsync(new JoinSessionRequest(session.JoinCode, "  Tým\t\n\r  Delta  "), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var team = await dbContext.Teams.SingleAsync(x => x.TeamId == result.Response!.TeamId);
        Assert.Equal("Tým Delta", team.Name);
        Assert.Equal("TÝM DELTA", team.NormalizedTeamName);
    }

    [Fact]
    public async Task JoinSessionAsync_TooLongTeamName_ReturnsValidationFailed()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var session = await CreateWaitingSessionAsync(quizService, CancellationToken.None);
        var tooLongTeamName = new string('X', 121);

        var result = await sessionService.JoinSessionAsync(new JoinSessionRequest(session.JoinCode, tooLongTeamName), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ApiErrorCode.ValidationFailed, result.Error!.Code);
    }

    [Fact]
    public async Task JoinSessionAsync_InvalidJoinCode_ReturnsNotFound()
    {
        await using var dbContext = CreateDbContext();
        var sessionService = CreateSessionService(dbContext);

        var result = await sessionService.JoinSessionAsync(new JoinSessionRequest("NEEXIST1", "Tým"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ApiErrorCode.ResourceNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task JoinSessionAsync_EmptyJoinCode_ReturnsSpecificValidationMessage()
    {
        await using var dbContext = CreateDbContext();
        var sessionService = CreateSessionService(dbContext);

        var result = await sessionService.JoinSessionAsync(new JoinSessionRequest(string.Empty, "Tým"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ApiErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Equal("Zadejte join kód", result.Error.Message);
    }

    [Fact]
    public async Task GetSessionStateAsync_InvalidReconnectToken_ReturnsInvalidAuthToken()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var session = await CreateWaitingSessionAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(session.JoinCode, "Tým Gama"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var stateResult = await sessionService.GetSessionStateAsync(session.SessionId, joinResult.Response!.TeamId, "spatny-token", CancellationToken.None);

        Assert.False(stateResult.IsSuccess);
        Assert.NotNull(stateResult.Error);
        Assert.Equal(ApiErrorCode.InvalidAuthToken, stateResult.Error!.Code);
    }

    [Fact]
    public async Task GetSessionStateAsync_ValidReconnectToken_ReturnsSnapshotWithTeams()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var session = await CreateWaitingSessionAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(session.JoinCode, "Tým Delta"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var stateResult = await sessionService.GetSessionStateAsync(
            session.SessionId,
            joinResult.Response!.TeamId,
            joinResult.Response.TeamReconnectToken,
            CancellationToken.None);

        Assert.True(stateResult.IsSuccess);
        Assert.NotNull(stateResult.Response);
        Assert.Equal(session.SessionId, stateResult.Response!.SessionId);
        Assert.Equal(SessionStatus.Waiting, stateResult.Response.Status);
        Assert.Null(stateResult.Response.CurrentQuestion);
        Assert.False(stateResult.Response.ResultsPublished);
        Assert.Contains(stateResult.Response.Teams, x => x.TeamId == joinResult.Response.TeamId && x.TeamName == "Tým Delta");
    }

    [Fact]
    public async Task HeartbeatTeamAsync_ValidToken_UpdatesLastSeen()
    {
        await using var dbContext = CreateDbContext();
        var sessionService = CreateSessionService(dbContext);

        var seeded = await SeedWaitingSessionWithTeamAsync(dbContext);

        var teamBeforeHeartbeat = await dbContext.Teams.SingleAsync(x => x.TeamId == seeded.TeamId);
        var lastSeenBeforeHeartbeat = teamBeforeHeartbeat.LastSeenAtUtc;

        await Task.Delay(25);

        var heartbeatResult = await sessionService.HeartbeatTeamAsync(
            seeded.SessionId,
            seeded.TeamId,
            seeded.TeamReconnectToken,
            CancellationToken.None);

        Assert.True(heartbeatResult.IsSuccess);

        var teamAfterHeartbeat = await dbContext.Teams.SingleAsync(x => x.TeamId == seeded.TeamId);
        Assert.True(teamAfterHeartbeat.LastSeenAtUtc > lastSeenBeforeHeartbeat);
    }

    [Fact]
    public async Task GetOrganizerSessionStateAsync_StaleTeam_IsReportedAsTemporarilyDisconnected()
    {
        await using var dbContext = CreateDbContext();
        var sessionService = CreateSessionService(dbContext);

        var seeded = await SeedWaitingSessionWithTeamAsync(dbContext);
        var team = await dbContext.Teams.SingleAsync(x => x.TeamId == seeded.TeamId);
        team.MarkSeen(DateTime.UtcNow.AddSeconds(-30));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var organizerSnapshot = await sessionService.GetOrganizerSessionStateAsync(seeded.SessionId, seeded.OrganizerToken, null, CancellationToken.None);

        Assert.True(organizerSnapshot.IsSuccess);
        var teamSnapshot = Assert.Single(organizerSnapshot.Response!.Teams);
        Assert.Equal(ParticipantPresenceStatus.TemporarilyDisconnected, teamSnapshot.PresenceStatus);
    }

    [Fact]
    public async Task HeartbeatOrganizerAsync_ValidAuth_WritesHeartbeatAudit()
    {
        await using var dbContext = CreateDbContext();
        var sessionService = CreateSessionService(dbContext);

        var seeded = await SeedWaitingSessionWithTeamAsync(dbContext);

        var heartbeatResult = await sessionService.HeartbeatOrganizerAsync(seeded.SessionId, seeded.OrganizerToken, null, CancellationToken.None);

        Assert.True(heartbeatResult.IsSuccess);
        Assert.True(await dbContext.AuditLogs.AnyAsync(x => x.SessionId == seeded.SessionId && x.ActionType == "ORGANIZER_HEARTBEAT"));
    }

    [Fact]
    public async Task GetOrganizerSessionStateAsync_StaleOrganizer_WritesSingleDisconnectedAudit()
    {
        await using var dbContext = CreateDbContext();
        var sessionService = CreateSessionService(dbContext);

        var seeded = await SeedWaitingSessionWithTeamAsync(dbContext);
        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == seeded.SessionId);
        var staleHeartbeatAtUtc = DateTime.UtcNow.AddSeconds(-30);

        dbContext.AuditLogs.Add(QuizApp.Server.Domain.Entities.AuditLog.Create(
            Guid.NewGuid(),
            staleHeartbeatAtUtc,
            "ORGANIZER_HEARTBEAT",
            session.QuizId,
            session.SessionId,
            "{}"));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var firstSnapshot = await sessionService.GetOrganizerSessionStateAsync(seeded.SessionId, seeded.OrganizerToken, null, CancellationToken.None);
        Assert.True(firstSnapshot.IsSuccess);

        var disconnectCountAfterFirstSnapshot = await dbContext.AuditLogs.CountAsync(
            x => x.SessionId == seeded.SessionId && x.ActionType == "ORGANIZER_DISCONNECTED");
        Assert.Equal(1, disconnectCountAfterFirstSnapshot);

        var secondSnapshot = await sessionService.GetOrganizerSessionStateAsync(seeded.SessionId, seeded.OrganizerToken, null, CancellationToken.None);
        Assert.True(secondSnapshot.IsSuccess);

        var disconnectCountAfterSecondSnapshot = await dbContext.AuditLogs.CountAsync(
            x => x.SessionId == seeded.SessionId && x.ActionType == "ORGANIZER_DISCONNECTED");
        Assert.Equal(1, disconnectCountAfterSecondSnapshot);
    }

    [Fact]
    public async Task GetOrganizerSessionStateAsync_ValidPassword_ReturnsSessionSnapshotWithTeams()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Omega"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var snapshotResult = await sessionService.GetOrganizerSessionStateAsync(created.SessionId, null, created.DeletePassword, CancellationToken.None);

        Assert.True(snapshotResult.IsSuccess);
        Assert.NotNull(snapshotResult.Response);
        Assert.Equal(created.SessionId, snapshotResult.Response!.SessionId);
        Assert.Equal(created.QuizId, snapshotResult.Response.QuizId);
        Assert.Equal(created.JoinCode, snapshotResult.Response.JoinCode);
        Assert.Equal(SessionStatus.Waiting, snapshotResult.Response.Status);
        Assert.Contains(snapshotResult.Response.Teams, x => x.TeamName == "Tým Omega");
    }

    [Fact]
    public async Task GetOrganizerSessionStateAsync_WithoutAuth_ReturnsSessionSnapshot()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);

        var snapshotResult = await sessionService.GetOrganizerSessionStateAsync(created.SessionId, null, null, CancellationToken.None);

        Assert.True(snapshotResult.IsSuccess);
        Assert.NotNull(snapshotResult.Response);
        Assert.Equal(created.SessionId, snapshotResult.Response!.SessionId);
    }

    [Fact]
    public async Task StartSessionAsync_WithoutTeams_ReturnsSessionStateChanged()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);

        var result = await sessionService.StartSessionAsync(created.SessionId, null, created.DeletePassword, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ApiErrorCode.SessionStateChanged, result.Error!.Code);
    }

    [Fact]
    public async Task StartSessionAsync_WithTeam_TransitionsToRunningAndWritesAudit()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Start"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);

        Assert.True(startResult.IsSuccess);
        Assert.NotNull(startResult.Response);
        Assert.Equal(SessionStatus.Running, startResult.Response!.Status);
        Assert.NotNull(startResult.Response.StartedAtUtc);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.Equal(SessionStatus.Running, session.Status);
        Assert.NotNull(session.StartedAtUtc);
        Assert.Equal(0, session.CurrentQuestionIndex);
        Assert.NotNull(session.CurrentQuestionStartedAtUtc);
        Assert.NotNull(session.QuestionDeadlineUtc);

        var audit = await dbContext.AuditLogs.SingleAsync(x => x.ActionType == "SESSION_STARTED");
        Assert.Equal(created.SessionId, audit.SessionId);
    }

    [Fact]
    public async Task StartSessionAsync_WithoutTimer_StartsWithQuestionWithoutDeadline()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Bez času"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None, useQuestionTimer: false);

        Assert.True(startResult.IsSuccess);
        Assert.NotNull(startResult.Response);
        Assert.Equal(SessionStatus.Running, startResult.Response!.Status);
        Assert.Null(startResult.Response.QuestionDeadlineUtc);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.Equal(SessionStatus.Running, session.Status);
        Assert.NotNull(session.CurrentQuestionStartedAtUtc);
        Assert.Null(session.QuestionDeadlineUtc);
    }

    [Fact]
    public async Task SubmitAnswerAsync_WithoutTimerMode_AllowsAnswerSubmission()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Odpověď"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None, useQuestionTimer: false);
        Assert.True(startResult.IsSuccess);

        var snapshotResult = await sessionService.GetSessionStateAsync(
            created.SessionId,
            joinResult.Response!.TeamId,
            joinResult.Response.TeamReconnectToken,
            CancellationToken.None);

        Assert.True(snapshotResult.IsSuccess);
        Assert.NotNull(snapshotResult.Response?.CurrentQuestion);

        var submitResult = await sessionService.SubmitAnswerAsync(
            created.SessionId,
            new SubmitAnswerRequest(joinResult.Response.TeamId, snapshotResult.Response!.CurrentQuestion!.QuestionId, OptionKey.B, null),
            joinResult.Response.TeamReconnectToken,
            CancellationToken.None);

        Assert.True(submitResult.IsSuccess);
    }

    [Fact]
    public async Task GetCurrentCorrectAnswerAsync_WithoutTimerRunningSession_ReturnsCorrectAnswer()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Ověření"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None, useQuestionTimer: false);
        Assert.True(startResult.IsSuccess);

        var correctAnswerResult = await sessionService.GetCurrentCorrectAnswerAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);

        Assert.True(correctAnswerResult.IsSuccess);
        Assert.NotNull(correctAnswerResult.Response);
        Assert.Equal(QuestionType.MultipleChoice, correctAnswerResult.Response!.QuestionType);
        Assert.Equal(OptionKey.B, correctAnswerResult.Response.CorrectOption);
    }

    [Fact]
    public async Task GetSessionStateAsync_AfterRevealCorrectAnswer_MarksCurrentQuestionAsClosedForTeams()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Uzavření"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None, useQuestionTimer: false);
        Assert.True(startResult.IsSuccess);

        var revealResult = await sessionService.GetCurrentCorrectAnswerAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(revealResult.IsSuccess);

        var stateResult = await sessionService.GetSessionStateAsync(
            created.SessionId,
            joinResult.Response!.TeamId,
            joinResult.Response.TeamReconnectToken,
            CancellationToken.None);

        Assert.True(stateResult.IsSuccess);
        Assert.NotNull(stateResult.Response);
        Assert.True(stateResult.Response!.IsCurrentQuestionAnsweringClosed);
    }

    [Fact]
    public async Task SubmitAnswerAsync_AfterRevealCorrectAnswer_ReturnsQuestionClosed()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Pozdní"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None, useQuestionTimer: false);
        Assert.True(startResult.IsSuccess);

        var snapshotResult = await sessionService.GetSessionStateAsync(
            created.SessionId,
            joinResult.Response!.TeamId,
            joinResult.Response.TeamReconnectToken,
            CancellationToken.None);

        Assert.True(snapshotResult.IsSuccess);
        Assert.NotNull(snapshotResult.Response?.CurrentQuestion);

        var revealResult = await sessionService.GetCurrentCorrectAnswerAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(revealResult.IsSuccess);

        var submitResult = await sessionService.SubmitAnswerAsync(
            created.SessionId,
            new SubmitAnswerRequest(joinResult.Response.TeamId, snapshotResult.Response!.CurrentQuestion!.QuestionId, OptionKey.B, null),
            joinResult.Response.TeamReconnectToken,
            CancellationToken.None);

        Assert.False(submitResult.IsSuccess);
        Assert.NotNull(submitResult.Error);
        Assert.Equal(ApiErrorCode.QuestionClosed, submitResult.Error!.Code);
    }

    [Fact]
    public async Task GetCurrentCorrectAnswerAsync_TimerMode_ReturnsSessionStateChanged()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Timer"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None, useQuestionTimer: true);
        Assert.True(startResult.IsSuccess);

        var correctAnswerResult = await sessionService.GetCurrentCorrectAnswerAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);

        Assert.False(correctAnswerResult.IsSuccess);
        Assert.NotNull(correctAnswerResult.Error);
        Assert.Equal(ApiErrorCode.SessionStateChanged, correctAnswerResult.Error!.Code);
    }

    [Fact]
    public async Task AdvanceSessionAsync_WithoutTimer_AdvancesAndFinishesOnLastQuestion()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithTwoQuestionsAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Ruční postup"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None, useQuestionTimer: false);
        Assert.True(startResult.IsSuccess);
        Assert.NotNull(startResult.Response);
        Assert.Equal(0, startResult.Response!.CurrentQuestionIndex);
        Assert.Equal(2, startResult.Response.TotalQuestionCount);
        Assert.Null(startResult.Response.QuestionDeadlineUtc);

        var advanceToSecond = await sessionService.AdvanceSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(advanceToSecond.IsSuccess);
        Assert.NotNull(advanceToSecond.Response);
        Assert.Equal(SessionStatus.Running, advanceToSecond.Response!.Status);
        Assert.Equal(1, advanceToSecond.Response.CurrentQuestionIndex);
        Assert.Null(advanceToSecond.Response.QuestionDeadlineUtc);

        var finishResult = await sessionService.AdvanceSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(finishResult.IsSuccess);
        Assert.NotNull(finishResult.Response);
        Assert.Equal(SessionStatus.Finished, finishResult.Response!.Status);

        var finishedSession = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.Equal(SessionStatus.Finished, finishedSession.Status);
        Assert.NotNull(finishedSession.FinishedAtUtc);
    }

    [Fact]
    public async Task PauseAndResumeSessionAsync_ShiftsQuestionTimingAndStatus()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Pause"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var startedSession = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        var initialDeadlineUtc = startedSession.QuestionDeadlineUtc;
        var initialQuestionStartedUtc = startedSession.CurrentQuestionStartedAtUtc;

        await Task.Delay(1100);

        var pauseResult = await sessionService.PauseSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(pauseResult.IsSuccess);
        Assert.Equal(SessionStatus.Paused, pauseResult.Response!.Status);

        await Task.Delay(1200);

        var resumeResult = await sessionService.ResumeSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(resumeResult.IsSuccess);
        Assert.Equal(SessionStatus.Running, resumeResult.Response!.Status);

        var resumedSession = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.NotNull(initialDeadlineUtc);
        Assert.NotNull(initialQuestionStartedUtc);
        Assert.True(resumedSession.QuestionDeadlineUtc > initialDeadlineUtc);
        Assert.True(resumedSession.CurrentQuestionStartedAtUtc > initialQuestionStartedUtc);
    }

    [Fact]
    public async Task ProgressDueSessionsAsync_ExpiredQuestion_AdvancesToNextQuestion()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithTwoQuestionsAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Progress"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        session.SetCurrentQuestion(0, DateTime.UtcNow.AddSeconds(-20), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        await sessionService.ProgressDueSessionsAsync(CancellationToken.None);

        var progressedSession = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.Equal(SessionStatus.Running, progressedSession.Status);
        Assert.Equal(1, progressedSession.CurrentQuestionIndex);
        Assert.NotNull(progressedSession.QuestionDeadlineUtc);
        Assert.True(progressedSession.QuestionDeadlineUtc > DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task ProgressDueSessionsAsync_LastQuestionExpired_FinishesSession()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithTwoQuestionsAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Finish"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        session.SetCurrentQuestion(1, DateTime.UtcNow.AddSeconds(-20), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        await sessionService.ProgressDueSessionsAsync(CancellationToken.None);

        var finishedSession = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.Equal(SessionStatus.Finished, finishedSession.Status);
        Assert.NotNull(finishedSession.FinishedAtUtc);
        Assert.NotNull(finishedSession.EndedAtUtc);
    }

    [Fact]
    public async Task CancelSessionAsync_WithoutExplicitConfirmation_ReturnsValidationFailed()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);

        var result = await sessionService.CancelSessionAsync(created.SessionId, null, created.DeletePassword, confirmCancellation: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ApiErrorCode.ValidationFailed, result.Error!.Code);
    }

    [Fact]
    public async Task CancelSessionAsync_RunningSession_TransitionsToCancelledAndWritesAudit()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Cancel"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var cancelResult = await sessionService.CancelSessionAsync(created.SessionId, null, created.DeletePassword, confirmCancellation: true, CancellationToken.None);

        Assert.True(cancelResult.IsSuccess);
        Assert.NotNull(cancelResult.Response);
        Assert.Equal(SessionStatus.Cancelled, cancelResult.Response!.Status);
        Assert.NotNull(cancelResult.Response.EndedAtUtc);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.Equal(SessionStatus.Cancelled, session.Status);
        Assert.NotNull(session.EndedAtUtc);

        var audit = await dbContext.AuditLogs.SingleAsync(x => x.ActionType == "SESSION_CANCELLED");
        Assert.Equal(created.SessionId, audit.SessionId);
    }

    [Fact]
    public async Task CancelledSession_ReleasesJoinCode_ForNextSessionCreation()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var originalJoinCode = created.JoinCode;

        var cancelResult = await sessionService.CancelSessionAsync(created.SessionId, created.OrganizerToken, null, confirmCancellation: true, CancellationToken.None);
        Assert.True(cancelResult.IsSuccess);

        var cancelledSession = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.Equal(SessionStatus.Cancelled, cancelledSession.Status);
        Assert.NotEqual(originalJoinCode, cancelledSession.JoinCode);

        var reusedJoinCodeSession = await quizService.CreateSessionAsync(
            created.QuizId,
            new CreateSessionRequest(originalJoinCode),
            created.OrganizerToken,
            null,
            CancellationToken.None);

        Assert.True(reusedJoinCodeSession.IsSuccess);
        Assert.NotNull(reusedJoinCodeSession.Response);
        Assert.Equal(originalJoinCode, reusedJoinCodeSession.Response!.JoinCode);
    }

    [Fact]
    public async Task CancelSessionAsync_TerminalState_ReturnsSessionStateChanged()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);

        var firstCancel = await sessionService.CancelSessionAsync(created.SessionId, created.OrganizerToken, null, confirmCancellation: true, CancellationToken.None);
        Assert.True(firstCancel.IsSuccess);

        var secondCancel = await sessionService.CancelSessionAsync(created.SessionId, created.OrganizerToken, null, confirmCancellation: true, CancellationToken.None);

        Assert.False(secondCancel.IsSuccess);
        Assert.NotNull(secondCancel.Error);
        Assert.Equal(ApiErrorCode.SessionStateChanged, secondCancel.Error!.Code);
    }

    [Fact]
    public async Task TerminateNonTerminalSessionsAsync_CancelsWaitingAndRunningSessions()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var waiting = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);
        var running = await CreateWaitingSessionWithTwoQuestionsAsync(quizService, CancellationToken.None);

        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(running.JoinCode, "Tým Startup"), CancellationToken.None);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(running.SessionId, running.OrganizerToken, null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var terminatedCount = await sessionService.TerminateNonTerminalSessionsAsync(CancellationToken.None);
        Assert.Equal(2, terminatedCount);

        var waitingAfter = await dbContext.Sessions.SingleAsync(x => x.SessionId == waiting.SessionId);
        Assert.Equal(SessionStatus.Cancelled, waitingAfter.Status);
        Assert.NotNull(waitingAfter.EndedAtUtc);
        Assert.NotEqual(waiting.JoinCode, waitingAfter.JoinCode);

        var runningAfter = await dbContext.Sessions.SingleAsync(x => x.SessionId == running.SessionId);
        Assert.Equal(SessionStatus.Cancelled, runningAfter.Status);
        Assert.NotNull(runningAfter.EndedAtUtc);
        Assert.NotEqual(running.JoinCode, runningAfter.JoinCode);
    }

    [Fact]
    public async Task SubmitAnswerAsync_ValidRequest_StoresAnswerWithCorrectnessAndResponseTime()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateRunningSessionWithTeamAsync(quizService, sessionService, CancellationToken.None);

        var submitResult = await sessionService.SubmitAnswerAsync(
            created.SessionId,
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B, null),
            created.TeamReconnectToken,
            CancellationToken.None);

        Assert.True(submitResult.IsSuccess);
        Assert.NotNull(submitResult.Response);
        Assert.Equal(created.SessionId, submitResult.Response!.SessionId);
        Assert.Equal(created.TeamId, submitResult.Response.TeamId);
        Assert.Equal(created.QuestionId, submitResult.Response.QuestionId);

        var answer = await dbContext.TeamAnswers.SingleAsync(x => x.SessionId == created.SessionId && x.TeamId == created.TeamId && x.QuestionId == created.QuestionId);
        Assert.True(answer.IsCorrect);
        Assert.True(answer.ResponseTimeMs >= 0);
    }

    [Fact]
    public async Task SubmitAnswerAsync_DuplicateSubmit_ReturnsAlreadyAnswered()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateRunningSessionWithTeamAsync(quizService, sessionService, CancellationToken.None);

        var firstSubmit = await sessionService.SubmitAnswerAsync(
            created.SessionId,
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B, null),
            created.TeamReconnectToken,
            CancellationToken.None);
        Assert.True(firstSubmit.IsSuccess);

        var secondSubmit = await sessionService.SubmitAnswerAsync(
            created.SessionId,
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B, null),
            created.TeamReconnectToken,
            CancellationToken.None);

        Assert.False(secondSubmit.IsSuccess);
        Assert.NotNull(secondSubmit.Error);
        Assert.Equal(ApiErrorCode.AlreadyAnswered, secondSubmit.Error!.Code);
    }

    [Fact]
    public async Task SubmitAnswerAsync_AfterDeadline_ReturnsQuestionClosed()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateRunningSessionWithTeamAsync(quizService, sessionService, CancellationToken.None);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        session.SetCurrentQuestion(0, DateTime.UtcNow.AddSeconds(-30), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var submitResult = await sessionService.SubmitAnswerAsync(
            created.SessionId,
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B, null),
            created.TeamReconnectToken,
            CancellationToken.None);

        Assert.False(submitResult.IsSuccess);
        Assert.NotNull(submitResult.Error);
        Assert.Equal(ApiErrorCode.QuestionClosed, submitResult.Error!.Code);
    }

    [Fact]
    public async Task SubmitAnswerAsync_InvalidReconnectToken_ReturnsInvalidAuthToken()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateRunningSessionWithTeamAsync(quizService, sessionService, CancellationToken.None);

        var submitResult = await sessionService.SubmitAnswerAsync(
            created.SessionId,
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B, null),
            "neplatny-token",
            CancellationToken.None);

        Assert.False(submitResult.IsSuccess);
        Assert.NotNull(submitResult.Error);
        Assert.Equal(ApiErrorCode.InvalidAuthToken, submitResult.Error!.Code);
    }

    [Fact]
    public async Task ProgressDueSessionsAsync_LastQuestionExpired_ComputesAndStoresSessionResults()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithTwoQuestionsAsync(quizService, CancellationToken.None);

        var join1 = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým A"), CancellationToken.None);
        Assert.True(join1.IsSuccess);
        var join2 = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým B"), CancellationToken.None);
        Assert.True(join2.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var snapshot1 = await sessionService.GetSessionStateAsync(created.SessionId, join1.Response!.TeamId, join1.Response.TeamReconnectToken, CancellationToken.None);
        var q1Id = snapshot1.Response!.CurrentQuestion!.QuestionId;

        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join1.Response.TeamId, q1Id, OptionKey.B, null), join1.Response.TeamReconnectToken, CancellationToken.None);
        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join2.Response!.TeamId, q1Id, OptionKey.A, null), join2.Response.TeamReconnectToken, CancellationToken.None);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        session.SetCurrentQuestion(0, DateTime.UtcNow.AddSeconds(-20), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        await sessionService.ProgressDueSessionsAsync(CancellationToken.None);

        session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.Equal(1, session.CurrentQuestionIndex);

        var snapshot2 = await sessionService.GetSessionStateAsync(created.SessionId, join1.Response.TeamId, join1.Response.TeamReconnectToken, CancellationToken.None);
        var q2Id = snapshot2.Response!.CurrentQuestion!.QuestionId;

        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join1.Response.TeamId, q2Id, OptionKey.B, null), join1.Response.TeamReconnectToken, CancellationToken.None);

        session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        session.SetCurrentQuestion(1, DateTime.UtcNow.AddSeconds(-20), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        await sessionService.ProgressDueSessionsAsync(CancellationToken.None);

        session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        Assert.Equal(SessionStatus.Finished, session.Status);

        var results = await dbContext.SessionResults.Where(x => x.SessionId == created.SessionId).ToListAsync();
        Assert.Equal(2, results.Count);

        var teamAResult = results.Single(x => x.TeamId == join1.Response.TeamId);
        Assert.Equal(2, teamAResult.Score);
        Assert.Equal(2, teamAResult.CorrectCount);
        Assert.Equal(1, teamAResult.Rank);

        var teamBResult = results.Single(x => x.TeamId == join2.Response.TeamId);
        Assert.Equal(0, teamBResult.Score);
        Assert.Equal(0, teamBResult.CorrectCount);
        Assert.Equal(2, teamBResult.Rank);
    }

    [Fact]
    public async Task GetSessionResultsAsync_FinishedSession_ReturnsRankedResults()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateFinishedSessionWithResultsAsync(quizService, sessionService, CancellationToken.None);

        var organizerPublishResult = await sessionService.GetSessionResultsAsync(
            created.SessionId,
            null,
            null,
            created.OrganizerToken,
            null,
            CancellationToken.None);
        Assert.True(organizerPublishResult.IsSuccess);

        var resultsResult = await sessionService.GetSessionResultsAsync(
            created.SessionId, created.Team1Id, created.Team1ReconnectToken, null, null, CancellationToken.None);

        Assert.True(resultsResult.IsSuccess);
        Assert.NotNull(resultsResult.Response);
        Assert.Equal(SessionStatus.Finished, resultsResult.Response!.Status);
        Assert.Equal(2, resultsResult.Response.Results.Count);
        Assert.Equal(1, resultsResult.Response.Results[0].Rank);
        Assert.True(resultsResult.Response.Results[0].Score >= resultsResult.Response.Results[1].Score);
    }

    [Fact]
    public async Task GetSessionResultsAsync_RunningSession_ReturnsSessionStateChanged()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateRunningSessionWithTeamAsync(quizService, sessionService, CancellationToken.None);

        var resultsResult = await sessionService.GetSessionResultsAsync(
            created.SessionId, created.TeamId, created.TeamReconnectToken, null, null, CancellationToken.None);

        Assert.False(resultsResult.IsSuccess);
        Assert.NotNull(resultsResult.Error);
        Assert.Equal(ApiErrorCode.SessionStateChanged, resultsResult.Error!.Code);
    }

    [Fact]
    public async Task GetSessionResultsAsync_OrganizerAuth_ReturnsResults()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateFinishedSessionWithResultsAsync(quizService, sessionService, CancellationToken.None);

        var resultsResult = await sessionService.GetSessionResultsAsync(
            created.SessionId, null, null, created.OrganizerToken, null, CancellationToken.None);

        Assert.True(resultsResult.IsSuccess);
        Assert.NotNull(resultsResult.Response);
        Assert.Equal(2, resultsResult.Response!.Results.Count);
    }

    [Fact]
    public async Task GetSessionResultsAsync_TeamsCanAccessOnlyAfterOrganizerPublishesResults()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var realtimePublisher = new FakeSessionRealtimePublisher();
        var sessionService = CreateSessionService(dbContext, realtimePublisher);

        var created = await CreateFinishedSessionWithResultsAsync(quizService, sessionService, CancellationToken.None);

        var teamBeforePublishResult = await sessionService.GetSessionResultsAsync(
            created.SessionId,
            created.Team1Id,
            created.Team1ReconnectToken,
            null,
            null,
            CancellationToken.None);

        Assert.False(teamBeforePublishResult.IsSuccess);
        Assert.NotNull(teamBeforePublishResult.Error);
        Assert.Equal(ApiErrorCode.SessionStateChanged, teamBeforePublishResult.Error!.Code);

        var organizerPublishResult = await sessionService.GetSessionResultsAsync(
            created.SessionId,
            null,
            null,
            created.OrganizerToken,
            null,
            CancellationToken.None);

        Assert.True(organizerPublishResult.IsSuccess);
        Assert.Contains(realtimePublisher.Events, x => x.SessionId == created.SessionId && x.EventName == RealtimeEventName.ResultsReady);

        var teamAfterPublishResult = await sessionService.GetSessionResultsAsync(
            created.SessionId,
            created.Team1Id,
            created.Team1ReconnectToken,
            null,
            null,
            CancellationToken.None);

        Assert.True(teamAfterPublishResult.IsSuccess);
        Assert.NotNull(teamAfterPublishResult.Response);
    }

    [Fact]
    public async Task GetSessionResultsAsync_NoAuth_ReturnsResults()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateFinishedSessionWithResultsAsync(quizService, sessionService, CancellationToken.None);

        var resultsResult = await sessionService.GetSessionResultsAsync(
            created.SessionId, null, null, null, null, CancellationToken.None);

        Assert.True(resultsResult.IsSuccess);
        Assert.NotNull(resultsResult.Response);
        Assert.NotEmpty(resultsResult.Response!.Results);
    }

    [Fact]
    public async Task GetCorrectAnswersAsync_FinishedSession_ReturnsCorrectAnswers()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateFinishedSessionWithResultsAsync(quizService, sessionService, CancellationToken.None);

        var correctResult = await sessionService.GetCorrectAnswersAsync(
            created.SessionId, null, null, created.OrganizerToken, null, CancellationToken.None);

        Assert.True(correctResult.IsSuccess);
        Assert.NotNull(correctResult.Response);
        Assert.Equal(2, correctResult.Response!.CorrectAnswers.Count);
        Assert.All(correctResult.Response.CorrectAnswers, a => Assert.Equal(OptionKey.B, a.CorrectOption));
    }

    [Fact]
    public async Task GetCorrectAnswersAsync_TeamAuth_ReturnsSelectedOptionForTeamAnswers()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateFinishedSessionWithResultsAsync(quizService, sessionService, CancellationToken.None);

        var organizerPublishResult = await sessionService.GetSessionResultsAsync(
            created.SessionId,
            null,
            null,
            created.OrganizerToken,
            null,
            CancellationToken.None);
        Assert.True(organizerPublishResult.IsSuccess);

        var correctResult = await sessionService.GetCorrectAnswersAsync(
            created.SessionId,
            created.Team2Id,
            created.Team2ReconnectToken,
            null,
            null,
            CancellationToken.None);

        Assert.True(correctResult.IsSuccess);
        Assert.NotNull(correctResult.Response);
        Assert.Equal(2, correctResult.Response!.CorrectAnswers.Count);
        Assert.Equal(OptionKey.A, correctResult.Response.CorrectAnswers[0].TeamSelectedOption);
        Assert.Null(correctResult.Response.CorrectAnswers[1].TeamSelectedOption);
    }

    [Fact]
    public async Task GetCorrectAnswersAsync_RunningSession_ReturnsSessionStateChanged()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateRunningSessionWithTeamAsync(quizService, sessionService, CancellationToken.None);

        var correctResult = await sessionService.GetCorrectAnswersAsync(
            created.SessionId, null, null, null, null, CancellationToken.None);

        Assert.False(correctResult.IsSuccess);
    }

    [Fact]
    public async Task GetCorrectAnswersAsync_WithoutOrganizerAuth_ReturnsCorrectAnswers()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateFinishedSessionWithResultsAsync(quizService, sessionService, CancellationToken.None);

        var correctResult = await sessionService.GetCorrectAnswersAsync(
            created.SessionId, null, null, null, null, CancellationToken.None);

        Assert.True(correctResult.IsSuccess);
        Assert.NotNull(correctResult.Response);
        Assert.NotEmpty(correctResult.Response!.CorrectAnswers);
    }

    [Fact]
    public async Task TieBreak_LowerTotalTimeWins()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);

        var join1 = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Rychlý tým"), CancellationToken.None);
        Assert.True(join1.IsSuccess);
        var join2 = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Pomalý tým"), CancellationToken.None);
        Assert.True(join2.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var snapshot = await sessionService.GetSessionStateAsync(created.SessionId, join1.Response!.TeamId, join1.Response.TeamReconnectToken, CancellationToken.None);
        var qId = snapshot.Response!.CurrentQuestion!.QuestionId;

        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join1.Response.TeamId, qId, OptionKey.B, null), join1.Response.TeamReconnectToken, CancellationToken.None);

        await Task.Delay(50);
        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join2.Response!.TeamId, qId, OptionKey.B, null), join2.Response.TeamReconnectToken, CancellationToken.None);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        session.SetCurrentQuestion(0, DateTime.UtcNow.AddSeconds(-40), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        await sessionService.ProgressDueSessionsAsync(CancellationToken.None);

        var results = await dbContext.SessionResults.Where(x => x.SessionId == created.SessionId).OrderBy(x => x.Rank).ToListAsync();
        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Score);
        Assert.Equal(1, results[1].Score);
        Assert.True(results[0].TotalCorrectResponseTimeMs <= results[1].TotalCorrectResponseTimeMs);
        Assert.Equal(1, results[0].Rank);
        Assert.Equal(2, results[1].Rank);
    }

    [Fact]
    public async Task NumericClosest_NearestButNotExact_GivesScoreButNotCorrectCount()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithSingleNumericQuestionAsync(quizService, CancellationToken.None);

        var join1 = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Nejblíž"), CancellationToken.None);
        Assert.True(join1.IsSuccess);
        var join2 = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Dál"), CancellationToken.None);
        Assert.True(join2.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var snapshot = await sessionService.GetSessionStateAsync(created.SessionId, join1.Response!.TeamId, join1.Response.TeamReconnectToken, CancellationToken.None);
        Assert.NotNull(snapshot.Response?.CurrentQuestion);
        var questionId = snapshot.Response!.CurrentQuestion!.QuestionId;

        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join1.Response.TeamId, questionId, null, 8.9m), join1.Response.TeamReconnectToken, CancellationToken.None);
        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join2.Response!.TeamId, questionId, null, 8.0m), join2.Response.TeamReconnectToken, CancellationToken.None);

        var session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId);
        session.SetCurrentQuestion(0, DateTime.UtcNow.AddSeconds(-20), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        await sessionService.ProgressDueSessionsAsync(CancellationToken.None);

        var results = await dbContext.SessionResults
            .Where(x => x.SessionId == created.SessionId)
            .ToListAsync();

        var nearestTeam = results.Single(x => x.TeamId == join1.Response.TeamId);
        Assert.Equal(1, nearestTeam.Score);
        Assert.Equal(0, nearestTeam.CorrectCount);

        var fartherTeam = results.Single(x => x.TeamId == join2.Response.TeamId);
        Assert.Equal(0, fartherTeam.Score);
        Assert.Equal(0, fartherTeam.CorrectCount);
    }

    private static QuizManagementService CreateQuizService(QuizAppDbContext dbContext)
    {
        return new QuizManagementService(dbContext, new QuizCsvParser());
    }

    private static SessionParticipationService CreateSessionService(QuizAppDbContext dbContext, ISessionRealtimePublisher? realtimePublisher = null)
    {
        return new SessionParticipationService(dbContext, realtimePublisher ?? new FakeSessionRealtimePublisher());
    }

    private static QuizAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<QuizAppDbContext>()
            .UseInMemoryDatabase($"session-tests-{Guid.NewGuid()}")
            .Options;

        return new QuizAppDbContext(options);
    }

    private static async Task<(Guid SessionId, string JoinCode)> CreateWaitingSessionAsync(QuizManagementService quizService, CancellationToken cancellationToken)
    {
        var createQuizResult = await quizService.CreateQuizAsync(new CreateQuizRequest("Session kvíz", "heslo"), cancellationToken);
        var quizId = createQuizResult.Response!.QuizId;
        var organizerToken = createQuizResult.Response.QuizOrganizerToken;

        var csv =
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

        var importResult = await quizService.ImportQuizCsvAsync(quizId, organizerToken, null, csv, cancellationToken);
        Assert.True(importResult.IsSuccess);

        var createSessionResult = await quizService.CreateSessionAsync(quizId, new CreateSessionRequest("ABCD2345"), organizerToken, null, cancellationToken);
        Assert.True(createSessionResult.IsSuccess);

        return (createSessionResult.Response!.SessionId, createSessionResult.Response.JoinCode);
    }

    private static async Task<(Guid QuizId, Guid SessionId, string JoinCode, string DeletePassword, string OrganizerToken)> CreateWaitingSessionWithQuizAuthAsync(QuizManagementService quizService, CancellationToken cancellationToken)
    {
        const string deletePassword = "heslo";
        var createQuizResult = await quizService.CreateQuizAsync(new CreateQuizRequest("Session kvíz", deletePassword), cancellationToken);
        var quizId = createQuizResult.Response!.QuizId;
        var organizerToken = createQuizResult.Response.QuizOrganizerToken;

        var csv =
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

        var importResult = await quizService.ImportQuizCsvAsync(quizId, organizerToken, null, csv, cancellationToken);
        Assert.True(importResult.IsSuccess);

        var createSessionResult = await quizService.CreateSessionAsync(quizId, new CreateSessionRequest("EFGH2345"), organizerToken, null, cancellationToken);
        Assert.True(createSessionResult.IsSuccess);

        return (quizId, createSessionResult.Response!.SessionId, createSessionResult.Response.JoinCode, deletePassword, organizerToken);
    }

    private static async Task<(Guid QuizId, Guid SessionId, string JoinCode, string DeletePassword, string OrganizerToken)> CreateWaitingSessionWithSingleNumericQuestionAsync(QuizManagementService quizService, CancellationToken cancellationToken)
    {
        const string deletePassword = "heslo";
        var createQuizResult = await quizService.CreateQuizAsync(new CreateQuizRequest("Numerický kvíz", deletePassword), cancellationToken);
        var quizId = createQuizResult.Response!.QuizId;
        var organizerToken = createQuizResult.Response.QuizOrganizerToken;

        var csv =
            "question_text;question_type;option_a;option_b;option_c;option_d;correct_option;correct_numeric_value;time_limit_sec\n" +
            "Kolik je 3x3?;numeric;;;;;;9;10\n";

        var importResult = await quizService.ImportQuizCsvAsync(quizId, organizerToken, null, csv, cancellationToken);
        Assert.True(importResult.IsSuccess);

        var createSessionResult = await quizService.CreateSessionAsync(quizId, new CreateSessionRequest("NUME2345"), organizerToken, null, cancellationToken);
        Assert.True(createSessionResult.IsSuccess);

        return (quizId, createSessionResult.Response!.SessionId, createSessionResult.Response.JoinCode, deletePassword, organizerToken);
    }

    private static async Task<(Guid SessionId, Guid TeamId, string TeamReconnectToken, Guid QuestionId)> CreateRunningSessionWithTeamAsync(
        QuizManagementService quizService,
        SessionParticipationService sessionService,
        CancellationToken cancellationToken)
    {
        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, cancellationToken);

        var joinResult = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Submit"), cancellationToken);
        Assert.True(joinResult.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, cancellationToken);
        Assert.True(startResult.IsSuccess);

        var snapshotResult = await sessionService.GetSessionStateAsync(
            created.SessionId,
            joinResult.Response!.TeamId,
            joinResult.Response.TeamReconnectToken,
            cancellationToken);
        Assert.True(snapshotResult.IsSuccess);
        Assert.NotNull(snapshotResult.Response);
        Assert.NotNull(snapshotResult.Response!.CurrentQuestion);

        return (
            created.SessionId,
            joinResult.Response.TeamId,
            joinResult.Response.TeamReconnectToken,
            snapshotResult.Response.CurrentQuestion!.QuestionId);
    }

    private static async Task<(Guid QuizId, Guid SessionId, string JoinCode, string DeletePassword, string OrganizerToken)> CreateWaitingSessionWithTwoQuestionsAsync(QuizManagementService quizService, CancellationToken cancellationToken)
    {
        const string deletePassword = "heslo";
        var createQuizResult = await quizService.CreateQuizAsync(new CreateQuizRequest("Session kvíz", deletePassword), cancellationToken);
        var quizId = createQuizResult.Response!.QuizId;
        var organizerToken = createQuizResult.Response.QuizOrganizerToken;

        var csv =
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;10\n" +
            "Kolik je 3+3?;5;6;7;8;B;10\n";

        var importResult = await quizService.ImportQuizCsvAsync(quizId, organizerToken, null, csv, cancellationToken);
        Assert.True(importResult.IsSuccess);

        var createSessionResult = await quizService.CreateSessionAsync(quizId, new CreateSessionRequest("JKLM2345"), organizerToken, null, cancellationToken);
        Assert.True(createSessionResult.IsSuccess);

        return (quizId, createSessionResult.Response!.SessionId, createSessionResult.Response.JoinCode, deletePassword, organizerToken);
    }

    private static async Task<(Guid SessionId, Guid Team1Id, string Team1ReconnectToken, Guid Team2Id, string Team2ReconnectToken, string OrganizerToken)> CreateFinishedSessionWithResultsAsync(
        QuizManagementService quizService,
        SessionParticipationService sessionService,
        CancellationToken cancellationToken)
    {
        var created = await CreateWaitingSessionWithTwoQuestionsAsync(quizService, cancellationToken);

        var join1 = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Výsledky A"), cancellationToken);
        Assert.True(join1.IsSuccess);
        var join2 = await sessionService.JoinSessionAsync(new JoinSessionRequest(created.JoinCode, "Tým Výsledky B"), cancellationToken);
        Assert.True(join2.IsSuccess);

        var startResult = await sessionService.StartSessionAsync(created.SessionId, created.OrganizerToken, null, cancellationToken);
        Assert.True(startResult.IsSuccess);

        var snapshot = await sessionService.GetSessionStateAsync(created.SessionId, join1.Response!.TeamId, join1.Response.TeamReconnectToken, cancellationToken);
        var q1Id = snapshot.Response!.CurrentQuestion!.QuestionId;

        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join1.Response.TeamId, q1Id, OptionKey.B, null), join1.Response.TeamReconnectToken, cancellationToken);
        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join2.Response!.TeamId, q1Id, OptionKey.A, null), join2.Response.TeamReconnectToken, cancellationToken);

        var dbContext = sessionService.GetType().GetField("_dbContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(sessionService) as QuizAppDbContext;
        var session = await dbContext!.Sessions.SingleAsync(x => x.SessionId == created.SessionId, cancellationToken);
        session.SetCurrentQuestion(0, DateTime.UtcNow.AddSeconds(-20), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(cancellationToken);

        await sessionService.ProgressDueSessionsAsync(cancellationToken);

        var snapshot2 = await sessionService.GetSessionStateAsync(created.SessionId, join1.Response.TeamId, join1.Response.TeamReconnectToken, cancellationToken);
        var q2Id = snapshot2.Response!.CurrentQuestion!.QuestionId;

        await sessionService.SubmitAnswerAsync(created.SessionId, new SubmitAnswerRequest(join1.Response.TeamId, q2Id, OptionKey.B, null), join1.Response.TeamReconnectToken, cancellationToken);

        session = await dbContext.Sessions.SingleAsync(x => x.SessionId == created.SessionId, cancellationToken);
        session.SetCurrentQuestion(1, DateTime.UtcNow.AddSeconds(-20), DateTime.UtcNow.AddSeconds(-1));
        await dbContext.SaveChangesAsync(cancellationToken);

        await sessionService.ProgressDueSessionsAsync(cancellationToken);

        return (created.SessionId, join1.Response.TeamId, join1.Response.TeamReconnectToken, join2.Response.TeamId, join2.Response.TeamReconnectToken, created.OrganizerToken);
    }

    private static async Task<(Guid SessionId, Guid TeamId, string TeamReconnectToken, string OrganizerToken)> SeedWaitingSessionWithTeamAsync(QuizAppDbContext dbContext)
    {
        var nowUtc = DateTime.UtcNow;
        var organizerToken = "ORGANIZER-R02";
        var teamReconnectToken = "TEAM-R02";

        var quiz = QuizApp.Server.Domain.Entities.Quiz.Create(
            Guid.NewGuid(),
            "Reconnect test quiz",
            "pbkdf2-sha256$10000$AA$BB",
            HashToken(organizerToken),
            nowUtc);

        var session = QuizApp.Server.Domain.Entities.QuizSession.Create(
            Guid.NewGuid(),
            quiz.QuizId,
            "R02JOIN",
            nowUtc);

        var team = QuizApp.Server.Domain.Entities.Team.Create(
            Guid.NewGuid(),
            session.SessionId,
            "Reconnect tým",
            HashToken(teamReconnectToken),
            nowUtc);

        dbContext.Quizzes.Add(quiz);
        dbContext.Sessions.Add(session);
        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return (session.SessionId, team.TeamId, teamReconnectToken, organizerToken);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private sealed class FakeSessionRealtimePublisher : ISessionRealtimePublisher
    {
        public List<(Guid SessionId, RealtimeEventName EventName)> Events { get; } = [];

        public Task PublishSessionEventAsync(Guid sessionId, RealtimeEventName eventName, CancellationToken cancellationToken)
        {
            Events.Add((sessionId, eventName));
            return Task.CompletedTask;
        }
    }
}
