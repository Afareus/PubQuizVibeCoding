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
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n" +
            "Barva oblohy?;Červená;Žlutá;Modrá;Zelená;C;25\n";

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
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

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
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

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
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

        await service.ImportQuizCsvAsync(createResult.Response!.QuizId, createResult.Response.QuizOrganizerToken, null, csv, CancellationToken.None);

        var detailResult = await service.GetQuizDetailAsync(createResult.Response.QuizId, null, "heslo", CancellationToken.None);

        Assert.True(detailResult.IsSuccess);
        Assert.NotNull(detailResult.Response);
        Assert.Equal("Detail test", detailResult.Response!.Name);
        Assert.Equal(1, detailResult.Response.QuestionCount);
        Assert.Single(detailResult.Response.Questions);
    }

    [Fact]
    public async Task GetQuizDetailAsync_WithoutAuthorization_ReturnsMetadataWithoutQuestions()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Detail bez auth", "heslo"), CancellationToken.None);

        var csv =
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

        await service.ImportQuizCsvAsync(createResult.Response!.QuizId, createResult.Response.QuizOrganizerToken, null, csv, CancellationToken.None);

        var detailResult = await service.GetQuizDetailAsync(createResult.Response.QuizId, null, null, CancellationToken.None);

        Assert.True(detailResult.IsSuccess);
        Assert.NotNull(detailResult.Response);
        Assert.Equal("Detail bez auth", detailResult.Response!.Name);
        Assert.Equal(1, detailResult.Response.QuestionCount);
        Assert.Empty(detailResult.Response.Questions);
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
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

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
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

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
    public async Task GenerateJoinCodeAsync_ValidPassword_ReturnsUnusedSixDigitCode()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Generování", "heslo123"), CancellationToken.None);

        dbContext.Sessions.Add(QuizApp.Server.Domain.Entities.QuizSession.Create(Guid.NewGuid(), createResult.Response!.QuizId, "123456", DateTime.UtcNow));
        await dbContext.SaveChangesAsync();

        var generateResult = await service.GenerateJoinCodeAsync(createResult.Response.QuizId, null, "heslo123", CancellationToken.None);

        Assert.True(generateResult.IsSuccess);
        Assert.NotNull(generateResult.Response);
        Assert.NotNull(generateResult.Response!.JoinCode);
        Assert.Equal(6, generateResult.Response.JoinCode.Length);
        Assert.Matches("^[0-9]{6}$", generateResult.Response.JoinCode);
        Assert.NotEqual("123456", generateResult.Response.JoinCode);
    }

    [Fact]
    public async Task GenerateJoinCodeAsync_WithoutAuthorization_ReturnsGeneratedCode()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Generování", "heslo123"), CancellationToken.None);

        var generateResult = await service.GenerateJoinCodeAsync(createResult.Response!.QuizId, null, null, CancellationToken.None);

        Assert.True(generateResult.IsSuccess);
        Assert.NotNull(generateResult.Response);
        Assert.NotNull(generateResult.Response!.JoinCode);
        Assert.Equal(6, generateResult.Response.JoinCode.Length);
        Assert.Matches("^[0-9]{6}$", generateResult.Response.JoinCode);
    }

    [Fact]
    public async Task CreateSessionAsync_IncompleteQuestionOrder_ReturnsValidationFailed()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Pořadí session", "heslo123"), CancellationToken.None);

        var firstQuestion = await service.AddQuestionAsync(
            createResult.Response!.QuizId,
            new AddQuizQuestionRequest("Q1", 30, QuestionType.MultipleChoice, OptionKey.A, null, "A", "B", "C", "D", 1),
            null,
            "heslo123",
            CancellationToken.None);

        var thirdQuestion = await service.AddQuestionAsync(
            createResult.Response.QuizId,
            new AddQuizQuestionRequest("Q3", 30, QuestionType.MultipleChoice, OptionKey.B, null, "A", "B", "C", "D", 3),
            null,
            "heslo123",
            CancellationToken.None);

        Assert.True(firstQuestion.IsSuccess);
        Assert.True(thirdQuestion.IsSuccess);

        var sessionResult = await service.CreateSessionAsync(
            createResult.Response.QuizId,
            new CreateSessionRequest("TEST1234"),
            null,
            "heslo123",
            CancellationToken.None);

        Assert.False(sessionResult.IsSuccess);
        Assert.NotNull(sessionResult.Error);
        Assert.Equal(ApiErrorCode.ValidationFailed, sessionResult.Error!.Code);
        Assert.Equal("Kvíz není možné spustit, protože neobsahuje kompletní pořadí otázek.", sessionResult.Error.Message);
    }

    [Fact]
    public async Task AddQuestionAsync_MultipleChoice_PersistsQuestionAndOptions()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Ruční otázka", "heslo123"), CancellationToken.None);

        var addResult = await service.AddQuestionAsync(
            createResult.Response!.QuizId,
            new AddQuizQuestionRequest(
                "Kolik je 3+4?",
                30,
                QuestionType.MultipleChoice,
                OptionKey.B,
                null,
                "6",
                "7",
                "8",
                "9"),
            null,
            "heslo123",
            CancellationToken.None);

        Assert.True(addResult.IsSuccess);
        Assert.NotNull(addResult.Response);
        Assert.Equal(0, addResult.Response!.OrderIndex);

        var question = await dbContext.Questions.SingleAsync(x => x.QuestionId == addResult.Response.QuestionId);
        Assert.Equal(QuestionType.MultipleChoice, question.QuestionType);
        Assert.Equal(OptionKey.B, question.CorrectOption);

        var options = await dbContext.QuestionOptions
            .Where(x => x.QuestionId == question.QuestionId)
            .OrderBy(x => x.OptionKey)
            .ToListAsync();

        Assert.Equal(4, options.Count);
        Assert.Equal("7", options[1].Text);
    }

    [Fact]
    public async Task AddQuestionAsync_NumericClosest_PersistsQuestionWithoutOptions()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Numerická otázka", "heslo123"), CancellationToken.None);

        var addResult = await service.AddQuestionAsync(
            createResult.Response!.QuizId,
            new AddQuizQuestionRequest(
                "Kolik měří Mount Everest?",
                45,
                QuestionType.NumericClosest,
                null,
                8848.86m,
                null,
                null,
                null,
                null),
            createResult.Response.QuizOrganizerToken,
            null,
            CancellationToken.None);

        Assert.True(addResult.IsSuccess);
        Assert.NotNull(addResult.Response);
        Assert.Equal(QuestionType.NumericClosest, addResult.Response!.QuestionType);

        var question = await dbContext.Questions.SingleAsync(x => x.QuestionId == addResult.Response.QuestionId);
        Assert.Equal(QuestionType.NumericClosest, question.QuestionType);
        Assert.Equal(8848.86m, question.CorrectNumericValue);
        Assert.Null(question.CorrectOption);

        var optionsCount = await dbContext.QuestionOptions.CountAsync(x => x.QuestionId == question.QuestionId);
        Assert.Equal(0, optionsCount);
    }

    [Fact]
    public async Task AddQuestionAsync_WithActiveSession_ReturnsQuizHasActiveSessions()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Aktivní session", "heslo123"), CancellationToken.None);

        var csv =
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

        await service.ImportQuizCsvAsync(createResult.Response!.QuizId, createResult.Response.QuizOrganizerToken, null, csv, CancellationToken.None);
        await service.CreateSessionAsync(createResult.Response.QuizId, new CreateSessionRequest("AKTIVNI01"), null, "heslo123", CancellationToken.None);

        var addResult = await service.AddQuestionAsync(
            createResult.Response.QuizId,
            new AddQuizQuestionRequest(
                "Další otázka",
                30,
                QuestionType.MultipleChoice,
                OptionKey.A,
                null,
                "A",
                "B",
                "C",
                "D"),
            null,
            "heslo123",
            CancellationToken.None);

        Assert.False(addResult.IsSuccess);
        Assert.NotNull(addResult.Error);
        Assert.Equal(ApiErrorCode.QuizHasActiveSessions, addResult.Error!.Code);
    }

    [Fact]
    public async Task AddQuestionAsync_DuplicateOrder_ReturnsValidationFailed()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Pořadí", "heslo123"), CancellationToken.None);

        var firstAdd = await service.AddQuestionAsync(
            createResult.Response!.QuizId,
            new AddQuizQuestionRequest(
                "První otázka",
                30,
                QuestionType.MultipleChoice,
                OptionKey.A,
                null,
                "A",
                "B",
                "C",
                "D",
                1),
            null,
            "heslo123",
            CancellationToken.None);

        Assert.True(firstAdd.IsSuccess);

        var secondAdd = await service.AddQuestionAsync(
            createResult.Response.QuizId,
            new AddQuizQuestionRequest(
                "Druhá otázka",
                30,
                QuestionType.MultipleChoice,
                OptionKey.B,
                null,
                "A",
                "B",
                "C",
                "D",
                1),
            null,
            "heslo123",
            CancellationToken.None);

        Assert.False(secondAdd.IsSuccess);
        Assert.NotNull(secondAdd.Error);
        Assert.Equal(ApiErrorCode.ValidationFailed, secondAdd.Error!.Code);
    }

    [Fact]
    public async Task UpdateQuestionAsync_ValidRequest_UpdatesQuestionAndOrder()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Editace", "heslo123"), CancellationToken.None);

        var firstQuestion = await service.AddQuestionAsync(
            createResult.Response!.QuizId,
            new AddQuizQuestionRequest("Q1", 30, QuestionType.MultipleChoice, OptionKey.A, null, "A1", "B1", "C1", "D1", 1),
            null,
            "heslo123",
            CancellationToken.None);

        var secondQuestion = await service.AddQuestionAsync(
            createResult.Response.QuizId,
            new AddQuizQuestionRequest("Q2", 30, QuestionType.MultipleChoice, OptionKey.B, null, "A2", "B2", "C2", "D2", 2),
            null,
            "heslo123",
            CancellationToken.None);

        Assert.True(firstQuestion.IsSuccess);
        Assert.True(secondQuestion.IsSuccess);

        var updateResult = await service.UpdateQuestionAsync(
            createResult.Response.QuizId,
            secondQuestion.Response!.QuestionId,
            new UpdateQuizQuestionRequest(
                "Q2 upravená",
                45,
                QuestionType.NumericClosest,
                null,
                42.5m,
                null,
                null,
                null,
                null,
                3),
            null,
            "heslo123",
            CancellationToken.None);

        Assert.True(updateResult.IsSuccess);

        var storedQuestion = await dbContext.Questions.SingleAsync(x => x.QuestionId == secondQuestion.Response.QuestionId);
        Assert.Equal("Q2 upravená", storedQuestion.Text);
        Assert.Equal(2, storedQuestion.OrderIndex);
        Assert.Equal(QuestionType.NumericClosest, storedQuestion.QuestionType);

        var optionsCount = await dbContext.QuestionOptions.CountAsync(x => x.QuestionId == storedQuestion.QuestionId);
        Assert.Equal(0, optionsCount);
    }

    [Fact]
    public async Task DeleteQuestionAsync_ValidRequest_DeletesQuestionAndReindexesOrder()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var createResult = await service.CreateQuizAsync(new CreateQuizRequest("Mazání otázky", "heslo123"), CancellationToken.None);

        var firstQuestion = await service.AddQuestionAsync(
            createResult.Response!.QuizId,
            new AddQuizQuestionRequest("Q1", 30, QuestionType.MultipleChoice, OptionKey.A, null, "A1", "B1", "C1", "D1", 1),
            null,
            "heslo123",
            CancellationToken.None);

        var secondQuestion = await service.AddQuestionAsync(
            createResult.Response.QuizId,
            new AddQuizQuestionRequest("Q2", 30, QuestionType.MultipleChoice, OptionKey.B, null, "A2", "B2", "C2", "D2", 2),
            null,
            "heslo123",
            CancellationToken.None);

        Assert.True(firstQuestion.IsSuccess);
        Assert.True(secondQuestion.IsSuccess);

        var deleteResult = await service.DeleteQuestionAsync(
            createResult.Response.QuizId,
            firstQuestion.Response!.QuestionId,
            null,
            "heslo123",
            CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);

        var remainingQuestions = await dbContext.Questions
            .Where(x => x.QuizId == createResult.Response.QuizId)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync();

        var remainingQuestion = Assert.Single(remainingQuestions);
        Assert.Equal(secondQuestion.Response!.QuestionId, remainingQuestion.QuestionId);
        Assert.Equal(0, remainingQuestion.OrderIndex);

        var deletedAudit = await dbContext.AuditLogs.SingleAsync(x => x.ActionType == "QUESTION_DELETED");
        Assert.Equal(createResult.Response.QuizId, deletedAudit.QuizId);
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
