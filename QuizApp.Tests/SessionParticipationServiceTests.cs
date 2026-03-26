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

    private static QuizManagementService CreateQuizService(QuizAppDbContext dbContext)
    {
        return new QuizManagementService(dbContext, new QuizCsvParser());
    }

    private static SessionParticipationService CreateSessionService(QuizAppDbContext dbContext)
    {
        return new SessionParticipationService(dbContext);
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

    private static async Task<(Guid QuizId, Guid SessionId, string JoinCode, string DeletePassword)> CreateWaitingSessionWithQuizAuthAsync(QuizManagementService quizService, CancellationToken cancellationToken)
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

        return (quizId, createSessionResult.Response!.SessionId, createSessionResult.Response.JoinCode, deletePassword);
    }
}
