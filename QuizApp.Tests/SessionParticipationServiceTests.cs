using Microsoft.EntityFrameworkCore;
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
        Assert.Contains(realtimePublisher.Events, x => x.SessionId == created.SessionId && x.EventName == RealtimeEventName.ResultsReady);
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
        Assert.Contains(stateResult.Response.Teams, x => x.TeamId == joinResult.Response.TeamId && x.TeamName == "Tým Delta");
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
    public async Task GetOrganizerSessionStateAsync_WithoutAuth_ReturnsMissingAuthToken()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateWaitingSessionWithQuizAuthAsync(quizService, CancellationToken.None);

        var snapshotResult = await sessionService.GetOrganizerSessionStateAsync(created.SessionId, null, null, CancellationToken.None);

        Assert.False(snapshotResult.IsSuccess);
        Assert.NotNull(snapshotResult.Error);
        Assert.Equal(ApiErrorCode.MissingAuthToken, snapshotResult.Error!.Code);
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
    public async Task SubmitAnswerAsync_ValidRequest_StoresAnswerWithCorrectnessAndResponseTime()
    {
        await using var dbContext = CreateDbContext();
        var quizService = CreateQuizService(dbContext);
        var sessionService = CreateSessionService(dbContext);

        var created = await CreateRunningSessionWithTeamAsync(quizService, sessionService, CancellationToken.None);

        var submitResult = await sessionService.SubmitAnswerAsync(
            created.SessionId,
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B),
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
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B),
            created.TeamReconnectToken,
            CancellationToken.None);
        Assert.True(firstSubmit.IsSuccess);

        var secondSubmit = await sessionService.SubmitAnswerAsync(
            created.SessionId,
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B),
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
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B),
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
            new SubmitAnswerRequest(created.TeamId, created.QuestionId, OptionKey.B),
            "neplatny-token",
            CancellationToken.None);

        Assert.False(submitResult.IsSuccess);
        Assert.NotNull(submitResult.Error);
        Assert.Equal(ApiErrorCode.InvalidAuthToken, submitResult.Error!.Code);
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
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n";

        var importResult = await quizService.ImportQuizCsvAsync(quizId, organizerToken, null, csv, cancellationToken);
        Assert.True(importResult.IsSuccess);

        var createSessionResult = await quizService.CreateSessionAsync(quizId, organizerToken, null, cancellationToken);
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
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n";

        var importResult = await quizService.ImportQuizCsvAsync(quizId, organizerToken, null, csv, cancellationToken);
        Assert.True(importResult.IsSuccess);

        var createSessionResult = await quizService.CreateSessionAsync(quizId, organizerToken, null, cancellationToken);
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
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,10\n" +
            "Kolik je 3+3?,5,6,7,8,B,10\n";

        var importResult = await quizService.ImportQuizCsvAsync(quizId, organizerToken, null, csv, cancellationToken);
        Assert.True(importResult.IsSuccess);

        var createSessionResult = await quizService.CreateSessionAsync(quizId, organizerToken, null, cancellationToken);
        Assert.True(createSessionResult.IsSuccess);

        return (quizId, createSessionResult.Response!.SessionId, createSessionResult.Response.JoinCode, deletePassword, organizerToken);
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
