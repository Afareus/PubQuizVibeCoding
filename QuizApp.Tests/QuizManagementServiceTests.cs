using Microsoft.EntityFrameworkCore;
using QuizApp.Server.Application.QuizImport;
using QuizApp.Server.Application.Quizzes;
using QuizApp.Server.Persistence;
using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Tests;

public class QuizManagementServiceTests
{
    [Fact]
    public async Task CreateQuizAsync_ValidRequest_PersistsQuizAndReturnsOneTimeToken()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateQuizAsync(new CreateQuizRequest("Hospodský kvíz", "tajneheslo"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Null(result.Error);
        Assert.Equal(64, result.Response!.QuizOrganizerToken.Length);

        var storedQuiz = await dbContext.Quizzes.SingleAsync(x => x.QuizId == result.Response.QuizId);
        Assert.Equal("Hospodský kvíz", storedQuiz.Name);
        Assert.NotEqual("tajneheslo", storedQuiz.DeletePasswordHash);
        Assert.NotEqual(result.Response.QuizOrganizerToken, storedQuiz.QuizOrganizerTokenHash);
        Assert.StartsWith("pbkdf2-sha256$", storedQuiz.DeletePasswordHash, StringComparison.Ordinal);

        var auditLog = await dbContext.AuditLogs.SingleAsync(x => x.ActionType == "QUIZ_CREATED");
        Assert.Equal(storedQuiz.QuizId, auditLog.QuizId);
    }

    [Fact]
    public async Task CreateQuizAsync_NameIsSanitizedBeforePersist()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateQuizAsync(new CreateQuizRequest("  Hospodský\t\n\r  kvíz  ", "tajneheslo"), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var storedQuiz = await dbContext.Quizzes.SingleAsync(x => x.QuizId == result.Response!.QuizId);
        Assert.Equal("Hospodský kvíz", storedQuiz.Name);
    }

    [Fact]
    public async Task CreateQuizAsync_TooLongName_ReturnsValidationFailed()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var tooLongName = new string('A', 201);
        var result = await service.CreateQuizAsync(new CreateQuizRequest(tooLongName, "tajneheslo"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ApiErrorCode.ValidationFailed, result.Error!.Code);
    }

    [Fact]
    public async Task ImportQuizCsvAsync_ValidCsv_ImportsQuestionsAndCreatesAuditLog()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Kvíz", "heslo"), CancellationToken.None);

        var csv =
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n" +
            "Barva oblohy?,Červená,Žlutá,Modrá,Zelená,C,25\n";

        var importResult = await service.ImportQuizCsvAsync(createResult.Response!.QuizId, createResult.Response.QuizOrganizerToken, null, csv, CancellationToken.None);

        Assert.True(importResult.IsSuccess);
        Assert.NotNull(importResult.Response);
        Assert.Empty(importResult.Response!.ValidationIssues);
        Assert.Equal(2, importResult.Response.ImportedQuestionsCount);

        var questions = await dbContext.Questions
            .Where(x => x.QuizId == createResult.Response.QuizId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync();

        Assert.Equal(2, questions.Count);
        Assert.Equal(0, questions[0].OrderIndex);
        Assert.Equal(1, questions[1].OrderIndex);

        var optionsCount = await dbContext.QuestionOptions.CountAsync();
        Assert.Equal(8, optionsCount);

        var importedAuditLog = await dbContext.AuditLogs.SingleAsync(x => x.ActionType == "QUIZ_IMPORTED");
        Assert.Equal(createResult.Response.QuizId, importedAuditLog.QuizId);
    }

    [Fact]
    public async Task ImportQuizCsvAsync_InvalidOrganizerToken_ReturnsInvalidAuthError()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Kvíz", "heslo"), CancellationToken.None);

        var csv =
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n";

        var importResult = await service.ImportQuizCsvAsync(createResult.Response!.QuizId, "spatny-token", null, csv, CancellationToken.None);

        Assert.False(importResult.IsSuccess);
        Assert.NotNull(importResult.Error);
        Assert.Equal(ApiErrorCode.InvalidAuthToken, importResult.Error!.Code);
        Assert.Null(importResult.Response);
        Assert.Empty(await dbContext.Questions.ToListAsync());
    }

    [Fact]
    public async Task ImportQuizCsvAsync_ValidPasswordWithoutToken_ImportsQuestions()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Kvíz", "heslo123"), CancellationToken.None);

        var csv =
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n";

        var importResult = await service.ImportQuizCsvAsync(createResult.Response!.QuizId, null, "heslo123", csv, CancellationToken.None);

        Assert.True(importResult.IsSuccess);
        Assert.NotNull(importResult.Response);
        Assert.Equal(1, importResult.Response!.ImportedQuestionsCount);
    }

    [Fact]
    public async Task GetQuizDetailAsync_ValidPassword_ReturnsQuizDetail()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Detail test", "heslo"), CancellationToken.None);

        var csv =
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n";

        await service.ImportQuizCsvAsync(createResult.Response!.QuizId, createResult.Response.QuizOrganizerToken, null, csv, CancellationToken.None);

        var detailResult = await service.GetQuizDetailAsync(createResult.Response.QuizId, null, "heslo", CancellationToken.None);

        Assert.True(detailResult.IsSuccess);
        Assert.NotNull(detailResult.Response);
        Assert.Equal("Detail test", detailResult.Response!.Name);
        Assert.Equal(1, detailResult.Response.QuestionCount);
        Assert.Single(detailResult.Response.Questions);
    }

    [Fact]
    public async Task CreateSessionAsync_QuizWithoutQuestions_ReturnsQuizHasNoQuestions()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Session test", "heslo"), CancellationToken.None);

        var sessionResult = await service.CreateSessionAsync(createResult.Response!.QuizId, new CreateSessionRequest("ABCD2345"), createResult.Response.QuizOrganizerToken, null, CancellationToken.None);

        Assert.False(sessionResult.IsSuccess);
        Assert.NotNull(sessionResult.Error);
        Assert.Equal(ApiErrorCode.QuizHasNoQuestions, sessionResult.Error!.Code);
    }

    [Fact]
    public async Task CreateSessionAsync_ValidPasswordWithoutToken_CreatesWaitingSessionAndAudit()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Session test", "heslo123"), CancellationToken.None);

        var csv =
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n";

        await service.ImportQuizCsvAsync(createResult.Response!.QuizId, createResult.Response.QuizOrganizerToken, null, csv, CancellationToken.None);

        var sessionResult = await service.CreateSessionAsync(createResult.Response.QuizId, new CreateSessionRequest("MNPR2345"), null, "heslo123", CancellationToken.None);

        Assert.True(sessionResult.IsSuccess);
        Assert.NotNull(sessionResult.Response);
        Assert.Equal(SessionStatus.Waiting, sessionResult.Response!.Status);
        Assert.Equal("MNPR2345", sessionResult.Response.JoinCode);

        var storedSession = await dbContext.Sessions.SingleAsync(x => x.SessionId == sessionResult.Response.SessionId);
        Assert.Equal(SessionStatus.Waiting, storedSession.Status);

        var audit = await dbContext.AuditLogs.SingleAsync(x => x.ActionType == "SESSION_CREATED");
        Assert.Equal(storedSession.SessionId, audit.SessionId);
        Assert.Equal(createResult.Response.QuizId, audit.QuizId);
    }

    [Fact]
    public async Task CreateSessionAsync_RepeatedCallsForSameQuiz_AreAllowed()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Session test", "heslo"), CancellationToken.None);

        var csv =
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n";

        await service.ImportQuizCsvAsync(createResult.Response!.QuizId, createResult.Response.QuizOrganizerToken, null, csv, CancellationToken.None);

        var firstSession = await service.CreateSessionAsync(createResult.Response.QuizId, new CreateSessionRequest("ABCD2345"), createResult.Response.QuizOrganizerToken, null, CancellationToken.None);
        var secondSession = await service.CreateSessionAsync(createResult.Response.QuizId, new CreateSessionRequest("EFGH2345"), createResult.Response.QuizOrganizerToken, null, CancellationToken.None);

        Assert.True(firstSession.IsSuccess);
        Assert.True(secondSession.IsSuccess);

        var sessions = await dbContext.Sessions
            .Where(x => x.QuizId == createResult.Response.QuizId)
            .ToListAsync();

        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, x => Assert.Equal(SessionStatus.Waiting, x.Status));
    }

    [Fact]
    public async Task DeleteQuizAsync_WithActiveSession_ReturnsConflict()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Mazání", "heslo"), CancellationToken.None);

        dbContext.Sessions.Add(QuizApp.Server.Domain.Entities.QuizSession.Create(Guid.NewGuid(), createResult.Response!.QuizId, "JOIN01", DateTime.UtcNow));
        await dbContext.SaveChangesAsync();

        var deleteResult = await service.DeleteQuizAsync(createResult.Response.QuizId, createResult.Response.QuizOrganizerToken, "heslo", CancellationToken.None);

        Assert.False(deleteResult.IsSuccess);
        Assert.NotNull(deleteResult.Error);
        Assert.Equal(ApiErrorCode.QuizHasActiveSessions, deleteResult.Error!.Code);
    }

    [Fact]
    public async Task DeleteQuizAsync_WrongPassword_ReturnsInvalidAuthToken()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Mazání", "spravne"), CancellationToken.None);

        var deleteResult = await service.DeleteQuizAsync(createResult.Response!.QuizId, createResult.Response.QuizOrganizerToken, "spatne", CancellationToken.None);

        Assert.False(deleteResult.IsSuccess);
        Assert.NotNull(deleteResult.Error);
        Assert.Equal(ApiErrorCode.InvalidAuthToken, deleteResult.Error!.Code);
    }

    [Fact]
    public async Task DeleteQuizAsync_ValidPassword_DeletesQuizAndWritesAudit()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Mazání", "heslo"), CancellationToken.None);

        var deleteResult = await service.DeleteQuizAsync(createResult.Response!.QuizId, null, "heslo", CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);

        var quizzesIncludingDeleted = await dbContext.Quizzes.IgnoreQueryFilters().ToListAsync();
        var quiz = Assert.Single(quizzesIncludingDeleted);
        Assert.True(quiz.IsDeleted);

        var deletedAudit = await dbContext.AuditLogs.SingleAsync(x => x.ActionType == "QUIZ_DELETED");
        Assert.Equal(quiz.QuizId, deletedAudit.QuizId);
    }

    private static QuizManagementService CreateService(QuizAppDbContext dbContext)
    {
        return new QuizManagementService(dbContext, new QuizCsvParser());
    }

    private static QuizAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<QuizAppDbContext>()
            .UseInMemoryDatabase($"quiz-tests-{Guid.NewGuid()}")
            .Options;

        return new QuizAppDbContext(options);
    }
}
