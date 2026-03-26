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
    public async Task ImportQuizCsvAsync_ValidCsv_ImportsQuestionsAndCreatesAuditLog()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Kvíz", "heslo"), CancellationToken.None);

        var csv =
            "question_text,option_a,option_b,option_c,option_d,correct_option,time_limit_sec\n" +
            "Kolik je 2+2?,3,4,5,6,B,30\n" +
            "Barva oblohy?,Červená,Žlutá,Modrá,Zelená,C,25\n";

        var importResult = await service.ImportQuizCsvAsync(createResult.Response!.QuizId, createResult.Response.QuizOrganizerToken, csv, CancellationToken.None);

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

        var importResult = await service.ImportQuizCsvAsync(createResult.Response!.QuizId, "spatny-token", csv, CancellationToken.None);

        Assert.False(importResult.IsSuccess);
        Assert.NotNull(importResult.Error);
        Assert.Equal(ApiErrorCode.InvalidAuthToken, importResult.Error!.Code);
        Assert.Null(importResult.Response);
        Assert.Empty(await dbContext.Questions.ToListAsync());
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
