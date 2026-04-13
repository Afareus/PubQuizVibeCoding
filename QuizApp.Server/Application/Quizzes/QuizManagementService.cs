using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuizApp.Server.Application.Common;
using QuizApp.Server.Application.QuizImport;
using QuizApp.Server.Domain.Entities;
using QuizApp.Server.Persistence;
using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Server.Application.Quizzes;

public interface IQuizManagementService
{
    Task<IReadOnlyList<QuizListItemResponse>> GetQuizzesAsync(CancellationToken cancellationToken);

    Task<CreateQuizOperationResult> CreateQuizAsync(CreateQuizRequest request, CancellationToken cancellationToken);

    Task<CreateSessionOperationResult> CreateSessionAsync(Guid quizId, CreateSessionRequest request, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<GenerateJoinCodeOperationResult> GenerateJoinCodeAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<AddQuizQuestionOperationResult> AddQuestionAsync(Guid quizId, AddQuizQuestionRequest request, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<AddQuizQuestionOperationResult> UpdateQuestionAsync(Guid quizId, Guid questionId, UpdateQuizQuestionRequest request, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<DeleteQuizQuestionOperationResult> DeleteQuestionAsync(Guid quizId, Guid questionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<ReorderQuizQuestionOperationResult> ReorderQuestionAsync(Guid quizId, ReorderQuizQuestionRequest request, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<ImportQuizCsvOperationResult> ImportQuizCsvAsync(Guid quizId, string? organizerToken, string? organizerPassword, string csvContent, CancellationToken cancellationToken);

    Task<QuizDetailOperationResult> GetQuizDetailAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<UpdateQuizStartPermissionOperationResult> UpdateQuizStartPermissionAsync(Guid quizId, UpdateQuizStartPermissionRequest request, string? organizerPassword, CancellationToken cancellationToken);

    Task<DeleteQuizOperationResult> DeleteQuizAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);
}

public sealed class QuizManagementService : IQuizManagementService
{
    private const int OrganizerTokenEntropyBytes = 32;
    private const int MinJoinCodeLength = 4;
    private const int GeneratedJoinCodeLength = 6;
    private const int GenerateJoinCodeMaxAttempts = 100;
    private const int PasswordSaltBytes = 16;
    private const int PasswordHashBytes = 32;
    private const int PasswordHashIterations = 100_000;
    private const int MaxQuizNameLength = 200;
    private const int MaxQuestionTextLength = 1500;
    private const int MaxQuestionOptionLength = 500;

    private readonly QuizAppDbContext _dbContext;
    private readonly IQuizCsvParser _quizCsvParser;

    public QuizManagementService(QuizAppDbContext dbContext, IQuizCsvParser quizCsvParser)
    {
        _dbContext = dbContext;
        _quizCsvParser = quizCsvParser;
    }

    public async Task<IReadOnlyList<QuizListItemResponse>> GetQuizzesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Quizzes
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new QuizListItemResponse(
                x.QuizId,
                x.Name,
                new DateTimeOffset(x.CreatedAtUtc, TimeSpan.Zero),
                x.IsStartAllowedForEveryone))
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateQuizOperationResult> CreateQuizAsync(CreateQuizRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateCreateQuizRequest(request);
        if (validationErrors is not null)
        {
            return CreateQuizOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Vstupní data nejsou validní.", validationErrors));
        }

        var nowUtc = DateTime.UtcNow;
        var quizId = Guid.NewGuid();
        var organizerToken = GenerateOrganizerToken();
        var sanitizedQuizName = TextInputSanitizer.SanitizeSingleLine(request.Name);

        var quiz = Quiz.Create(
            quizId,
            sanitizedQuizName,
            HashPassword(request.DeletePassword),
            HashOrganizerToken(organizerToken),
            nowUtc);

        var createdAuditLog = AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "QUIZ_CREATED",
            quiz.QuizId,
            null,
            JsonSerializer.Serialize(new QuizCreatedAuditPayload(quiz.QuizId, quiz.Name)));

        _dbContext.Quizzes.Add(quiz);
        _dbContext.AuditLogs.Add(createdAuditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreateQuizOperationResult.Success(new CreateQuizResponse(quiz.QuizId, organizerToken));
    }

    public async Task<CreateSessionOperationResult> CreateSessionAsync(Guid quizId, CreateSessionRequest request, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateCreateSessionRequest(request);
        if (validationErrors is not null)
        {
            return CreateSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Vstupní data nejsou validní.", validationErrors));
        }

        var normalizedJoinCode = request.JoinCode.Trim().ToUpperInvariant();

        var quiz = await _dbContext.Quizzes
            .Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return CreateSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (!quiz.IsStartAllowedForEveryone)
        {
            return CreateSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuizStartLocked, "Spouštění tohoto kvízu je aktuálně uzamčeno administrátorem."));
        }

        if (quiz.Questions.Count == 0)
        {
            return CreateSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuizHasNoQuestions, "Nelze spustit kvíz, který neobsahuje žádné otázky."));
        }

        if (!HasCompleteQuestionOrder(quiz.Questions))
        {
            return CreateSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Kvíz není možné spustit, protože neobsahuje kompletní pořadí otázek."));
        }

        var joinCodeAlreadyUsed = await _dbContext.Sessions.AnyAsync(x => x.JoinCode == normalizedJoinCode, cancellationToken);
        if (joinCodeAlreadyUsed)
        {
            return CreateSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Zadaný join kód už je použitý v jiné spuštěné hře."));
        }

        var nowUtc = DateTime.UtcNow;
        var session = QuizSession.Create(Guid.NewGuid(), quiz.QuizId, normalizedJoinCode, nowUtc);

        _dbContext.Sessions.Add(session);
        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "SESSION_CREATED",
            quiz.QuizId,
            session.SessionId,
            JsonSerializer.Serialize(new SessionCreatedAuditPayload(session.SessionId, quiz.QuizId, session.JoinCode))));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return CreateSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Zadaný join kód už je použitý v jiné spuštěné hře."));
        }

        return CreateSessionOperationResult.Success(new CreateSessionResponse(session.SessionId, session.JoinCode, session.Status));
    }

    public async Task<GenerateJoinCodeOperationResult> GenerateJoinCodeAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var quiz = await _dbContext.Quizzes
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return GenerateJoinCodeOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        for (var attempt = 0; attempt < GenerateJoinCodeMaxAttempts; attempt++)
        {
            var candidateJoinCode = GenerateNumericJoinCode(GeneratedJoinCodeLength);
            var joinCodeAlreadyUsed = await _dbContext.Sessions.AnyAsync(x => x.JoinCode == candidateJoinCode, cancellationToken);
            if (!joinCodeAlreadyUsed)
            {
                return GenerateJoinCodeOperationResult.Success(new GenerateJoinCodeResponse(candidateJoinCode));
            }
        }

        return GenerateJoinCodeOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Nepodařilo se vygenerovat volný join kód. Zkuste to prosím znovu."));
    }

    public async Task<AddQuizQuestionOperationResult> AddQuestionAsync(Guid quizId, AddQuizQuestionRequest request, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateAddQuestionRequest(request);
        if (validationErrors is not null)
        {
            return AddQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Vstupní data nejsou validní.", validationErrors));
        }

        var quiz = await _dbContext.Quizzes
            .Include(x => x.Questions)
            .Include(x => x.Sessions)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return AddQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (!TryAuthorizeOrganizer(quiz, organizerToken, organizerPassword, out var authError))
        {
            return AddQuizQuestionOperationResult.Fail(authError!);
        }

        if (quiz.Sessions.Any(session => session.Status is SessionStatus.Waiting or SessionStatus.Running))
        {
            return AddQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuizHasActiveSessions, "Otázky nelze upravovat, protože kvíz má aktivní hru."));
        }

        var desiredOrder = request.Order ?? (quiz.Questions.Count + 1);
        var desiredOrderIndex = desiredOrder - 1;

        if (quiz.Questions.Any(question => question.OrderIndex == desiredOrderIndex))
        {
            return AddQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Otázka se zadaným pořadím už existuje."));
        }

        var questionId = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;
        var sanitizedText = TextInputSanitizer.SanitizeSingleLine(request.Text);

        var question = Question.Create(
            questionId,
            quiz.QuizId,
            desiredOrderIndex,
            sanitizedText,
            request.TimeLimitSec,
            request.QuestionType,
            request.CorrectOption,
            request.CorrectNumericValue);

        _dbContext.Questions.Add(question);

        if (request.QuestionType == QuestionType.MultipleChoice)
        {
            _dbContext.QuestionOptions.AddRange(
                QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.A, TextInputSanitizer.SanitizeSingleLine(request.OptionA)),
                QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.B, TextInputSanitizer.SanitizeSingleLine(request.OptionB)),
                QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.C, TextInputSanitizer.SanitizeSingleLine(request.OptionC)),
                QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.D, TextInputSanitizer.SanitizeSingleLine(request.OptionD)));
        }

        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "QUESTION_ADDED",
            quiz.QuizId,
            null,
            JsonSerializer.Serialize(new QuestionAddedAuditPayload(quiz.QuizId, question.QuestionId, question.OrderIndex, question.QuestionType))));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AddQuizQuestionOperationResult.Success(new AddQuizQuestionResponse(question.QuestionId, question.OrderIndex, question.QuestionType));
    }

    public async Task<DeleteQuizQuestionOperationResult> DeleteQuestionAsync(Guid quizId, Guid questionId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var quiz = await _dbContext.Quizzes
            .Include(x => x.Questions)
            .Include(x => x.Sessions)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return DeleteQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (!TryAuthorizeOrganizer(quiz, organizerToken, organizerPassword, out var authError))
        {
            return DeleteQuizQuestionOperationResult.Fail(authError!);
        }

        if (quiz.Sessions.Any(session => session.Status is SessionStatus.Waiting or SessionStatus.Running))
        {
            return DeleteQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuizHasActiveSessions, "Otázky nelze upravovat, protože kvíz má aktivní hru."));
        }

        var question = quiz.Questions.SingleOrDefault(x => x.QuestionId == questionId);
        if (question is null)
        {
            return DeleteQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Otázka nebyla nalezena."));
        }

        var deletedOrderIndex = question.OrderIndex;
        _dbContext.Questions.Remove(question);

        foreach (var nextQuestion in quiz.Questions.Where(x => x.QuestionId != questionId && x.OrderIndex > deletedOrderIndex))
        {
            nextQuestion.SetOrderIndex(nextQuestion.OrderIndex - 1);
        }

        var nowUtc = DateTime.UtcNow;
        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "QUESTION_DELETED",
            quiz.QuizId,
            null,
            JsonSerializer.Serialize(new QuestionDeletedAuditPayload(quiz.QuizId, question.QuestionId, deletedOrderIndex))));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return DeleteQuizQuestionOperationResult.Success();
    }

    public async Task<AddQuizQuestionOperationResult> UpdateQuestionAsync(Guid quizId, Guid questionId, UpdateQuizQuestionRequest request, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateUpdateQuestionRequest(request);
        if (validationErrors is not null)
        {
            return AddQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Vstupní data nejsou validní.", validationErrors));
        }

        var quiz = await _dbContext.Quizzes
            .Include(x => x.Questions)
            .ThenInclude(x => x.Options)
            .Include(x => x.Sessions)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return AddQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (!TryAuthorizeOrganizer(quiz, organizerToken, organizerPassword, out var authError))
        {
            return AddQuizQuestionOperationResult.Fail(authError!);
        }

        if (quiz.Sessions.Any(session => session.Status is SessionStatus.Waiting or SessionStatus.Running))
        {
            return AddQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuizHasActiveSessions, "Otázky nelze upravovat, protože kvíz má aktivní hru."));
        }

        var question = quiz.Questions.SingleOrDefault(x => x.QuestionId == questionId);
        if (question is null)
        {
            return AddQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Otázka nebyla nalezena."));
        }

        var desiredOrderIndex = request.Order - 1;
        if (quiz.Questions.Any(existingQuestion => existingQuestion.QuestionId != questionId && existingQuestion.OrderIndex == desiredOrderIndex))
        {
            return AddQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Otázka se zadaným pořadím už existuje."));
        }

        var sanitizedText = TextInputSanitizer.SanitizeSingleLine(request.Text);
        question.Update(
            desiredOrderIndex,
            sanitizedText,
            request.TimeLimitSec,
            request.QuestionType,
            request.CorrectOption,
            request.CorrectNumericValue);

        if (request.QuestionType == QuestionType.MultipleChoice)
        {
            var sanitizedOptions = new Dictionary<OptionKey, string>
            {
                [OptionKey.A] = TextInputSanitizer.SanitizeSingleLine(request.OptionA),
                [OptionKey.B] = TextInputSanitizer.SanitizeSingleLine(request.OptionB),
                [OptionKey.C] = TextInputSanitizer.SanitizeSingleLine(request.OptionC),
                [OptionKey.D] = TextInputSanitizer.SanitizeSingleLine(request.OptionD)
            };

            var existingOptions = question.Options.ToDictionary(x => x.OptionKey);
            foreach (var (optionKey, optionText) in sanitizedOptions)
            {
                if (existingOptions.TryGetValue(optionKey, out var option))
                {
                    option.UpdateText(optionText);
                }
                else
                {
                    _dbContext.QuestionOptions.Add(QuestionOption.Create(Guid.NewGuid(), question.QuestionId, optionKey, optionText));
                }
            }
        }
        else
        {
            if (question.Options.Count > 0)
            {
                _dbContext.QuestionOptions.RemoveRange(question.Options);
            }
        }

        var nowUtc = DateTime.UtcNow;
        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "QUESTION_UPDATED",
            quiz.QuizId,
            null,
            JsonSerializer.Serialize(new QuestionUpdatedAuditPayload(quiz.QuizId, question.QuestionId, question.OrderIndex, question.QuestionType))));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AddQuizQuestionOperationResult.Success(new AddQuizQuestionResponse(question.QuestionId, question.OrderIndex, question.QuestionType));
    }

    public async Task<ReorderQuizQuestionOperationResult> ReorderQuestionAsync(Guid quizId, ReorderQuizQuestionRequest request, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        if (request.Direction is not (1 or -1))
        {
            return ReorderQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Směr přesunu musí být -1 (nahoru) nebo 1 (dolů)."));
        }

        var quiz = await _dbContext.Quizzes
            .Include(x => x.Questions)
            .Include(x => x.Sessions)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return ReorderQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (!TryAuthorizeOrganizer(quiz, organizerToken, organizerPassword, out var authError))
        {
            return ReorderQuizQuestionOperationResult.Fail(authError!);
        }

        if (quiz.Sessions.Any(session => session.Status is SessionStatus.Waiting or SessionStatus.Running))
        {
            return ReorderQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuizHasActiveSessions, "Pořadí otázek nelze měnit, protože kvíz má aktivní hru."));
        }

        var question = quiz.Questions.SingleOrDefault(x => x.QuestionId == request.QuestionId);
        if (question is null)
        {
            return ReorderQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Otázka nebyla nalezena."));
        }

        var targetOrderIndex = question.OrderIndex + request.Direction;
        var neighbor = quiz.Questions.SingleOrDefault(x => x.OrderIndex == targetOrderIndex);

        if (neighbor is null)
        {
            return ReorderQuizQuestionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Otázku nelze přesunout tímto směrem."));
        }

        var originalIndex = question.OrderIndex;

        if (_dbContext.Database.IsRelational())
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Questions" SET "OrderIndex" = -1 WHERE "QuestionId" = {question.QuestionId}
                """, cancellationToken);

            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Questions" SET "OrderIndex" = {originalIndex} WHERE "QuestionId" = {neighbor.QuestionId}
                """, cancellationToken);

            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Questions" SET "OrderIndex" = {targetOrderIndex} WHERE "QuestionId" = {question.QuestionId}
                """, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            question.SetOrderIndex(targetOrderIndex);
            neighbor.SetOrderIndex(originalIndex);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ReorderQuizQuestionOperationResult.Success();
    }

    public async Task<ImportQuizCsvOperationResult> ImportQuizCsvAsync(Guid quizId, string? organizerToken, string? organizerPassword, string csvContent, CancellationToken cancellationToken)
    {
        var quiz = await _dbContext.Quizzes
            .Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return ImportQuizCsvOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (!TryAuthorizeOrganizer(quiz, organizerToken, organizerPassword, out var authError))
        {
            return ImportQuizCsvOperationResult.Fail(authError!);
        }

        if (quiz.Questions.Count > 0)
        {
            return ImportQuizCsvOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ValidationFailed, "Kvíz už obsahuje otázky a nelze ho importovat znovu."));
        }

        var parseResult = _quizCsvParser.Parse(csvContent);
        if (!parseResult.IsValid)
        {
            return ImportQuizCsvOperationResult.Success(new ImportQuizCsvResponse(0, parseResult.ValidationIssues));
        }

        if (parseResult.Questions.Count == 0)
        {
            var issues = new[]
            {
                new CsvValidationIssueDto(1, "row", ApiErrorCode.CsvValidationFailed, "CSV musí obsahovat alespoň jednu otázku.")
            };

            return ImportQuizCsvOperationResult.Success(new ImportQuizCsvResponse(0, issues));
        }

        var nowUtc = DateTime.UtcNow;
        var questions = new List<Question>(parseResult.Questions.Count);
        var options = new List<QuestionOption>(parseResult.Questions.Count * 4);

        for (var index = 0; index < parseResult.Questions.Count; index++)
        {
            var parsedQuestion = parseResult.Questions[index];
            var questionId = Guid.NewGuid();

            questions.Add(Question.Create(
                questionId,
                quiz.QuizId,
                index,
                parsedQuestion.QuestionText,
                parsedQuestion.TimeLimitSec,
                parsedQuestion.QuestionType,
                parsedQuestion.CorrectOption,
                parsedQuestion.CorrectNumericValue));

            if (parsedQuestion.QuestionType == QuestionType.MultipleChoice)
            {
                options.Add(QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.A, parsedQuestion.OptionA));
                options.Add(QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.B, parsedQuestion.OptionB));
                options.Add(QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.C, parsedQuestion.OptionC));
                options.Add(QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.D, parsedQuestion.OptionD));
            }
        }

        _dbContext.Questions.AddRange(questions);
        _dbContext.QuestionOptions.AddRange(options);
        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "QUIZ_IMPORTED",
            quiz.QuizId,
            null,
            JsonSerializer.Serialize(new QuizImportedAuditPayload(quiz.QuizId, questions.Count))));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ImportQuizCsvOperationResult.Success(new ImportQuizCsvResponse(questions.Count, []));
    }

    public async Task<QuizDetailOperationResult> GetQuizDetailAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var quiz = await _dbContext.Quizzes
            .AsNoTracking()
            .Include(x => x.Questions)
            .ThenInclude(x => x.Options)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return QuizDetailOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (string.IsNullOrWhiteSpace(organizerToken) && string.IsNullOrWhiteSpace(organizerPassword))
        {
            return QuizDetailOperationResult.Success(new QuizDetailResponse(
                quiz.QuizId,
                quiz.Name,
                new DateTimeOffset(quiz.CreatedAtUtc, TimeSpan.Zero),
                quiz.Questions.Count,
                quiz.IsStartAllowedForEveryone,
                []));
        }

        if (!TryAuthorizeOrganizer(quiz, organizerToken, organizerPassword, out var authError))
        {
            return QuizDetailOperationResult.Fail(authError!);
        }

        var questions = quiz.Questions
            .OrderBy(x => x.OrderIndex)
            .Select(question => new QuizDetailQuestionDto(
                question.QuestionId,
                question.OrderIndex,
                question.Text,
                question.TimeLimitSec,
                question.QuestionType,
                question.CorrectOption,
                question.CorrectNumericValue,
                question.Options
                    .OrderBy(option => option.OptionKey)
                    .Select(option => new QuizDetailQuestionOptionDto(option.OptionKey, option.Text))
                    .ToList()))
            .ToList();

        return QuizDetailOperationResult.Success(new QuizDetailResponse(
            quiz.QuizId,
            quiz.Name,
            new DateTimeOffset(quiz.CreatedAtUtc, TimeSpan.Zero),
            questions.Count,
            quiz.IsStartAllowedForEveryone,
            questions));
    }

    public async Task<UpdateQuizStartPermissionOperationResult> UpdateQuizStartPermissionAsync(Guid quizId, UpdateQuizStartPermissionRequest request, string? organizerPassword, CancellationToken cancellationToken)
    {
        var quiz = await _dbContext.Quizzes
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return UpdateQuizStartPermissionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (string.IsNullOrWhiteSpace(organizerPassword))
        {
            return UpdateQuizStartPermissionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.MissingAuthToken, "Chybí Administrátorké heslo kvízu v hlavičce X-Quiz-Password."));
        }

        if (!VerifyPassword(organizerPassword, quiz.DeletePasswordHash))
        {
            return UpdateQuizStartPermissionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatné Administrátorké heslo kvízu."));
        }

        quiz.SetStartPermission(request.IsStartAllowedForEveryone);

        var nowUtc = DateTime.UtcNow;
        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "QUIZ_START_PERMISSION_UPDATED",
            quiz.QuizId,
            null,
            JsonSerializer.Serialize(new QuizStartPermissionUpdatedAuditPayload(quiz.QuizId, request.IsStartAllowedForEveryone))));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return UpdateQuizStartPermissionOperationResult.Success(new UpdateQuizStartPermissionResponse(quiz.QuizId, quiz.IsStartAllowedForEveryone));
    }

    public async Task<DeleteQuizOperationResult> DeleteQuizAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var quiz = await _dbContext.Quizzes
            .Include(x => x.Sessions)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return DeleteQuizOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (string.IsNullOrWhiteSpace(organizerPassword))
        {
            return DeleteQuizOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.MissingAuthToken, "Chybí Administrátorké heslo kvízu v hlavičce X-Quiz-Password."));
        }

        if (!TryAuthorizeOrganizer(quiz, organizerToken, organizerPassword, out var authError))
        {
            return DeleteQuizOperationResult.Fail(authError!);
        }

        if (!VerifyPassword(organizerPassword, quiz.DeletePasswordHash))
        {
            return DeleteQuizOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatné Administrátorké heslo kvízu."));
        }

        if (quiz.Sessions.Any(session => session.Status is SessionStatus.Waiting or SessionStatus.Running))
        {
            return DeleteQuizOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuizHasActiveSessions, "Kvíz nelze smazat, protože má aktivní session."));
        }

        var nowUtc = DateTime.UtcNow;
        quiz.MarkAsDeleted(nowUtc);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "QUIZ_DELETED",
            quiz.QuizId,
            null,
            JsonSerializer.Serialize(new QuizDeletedAuditPayload(quiz.QuizId))));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return DeleteQuizOperationResult.Success();
    }

    private static IReadOnlyDictionary<string, string[]>? ValidateCreateQuizRequest(CreateQuizRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var sanitizedName = TextInputSanitizer.SanitizeSingleLine(request.Name);

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            errors[nameof(CreateQuizRequest.Name)] = ["Název kvízu je povinný."];
        }
        else if (sanitizedName.Length > MaxQuizNameLength)
        {
            errors[nameof(CreateQuizRequest.Name)] = [$"Název kvízu může mít maximálně {MaxQuizNameLength} znaků."];
        }

        if (string.IsNullOrWhiteSpace(request.DeletePassword))
        {
            errors[nameof(CreateQuizRequest.DeletePassword)] = ["Administrátorké heslo kvízu je povinné."];
        }

        return errors.Count == 0 ? null : errors;
    }

    private static string GenerateNumericJoinCode(int length)
    {
        Span<char> buffer = stackalloc char[length];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(buffer);
    }

    private static bool HasCompleteQuestionOrder(IReadOnlyCollection<Question> questions)
    {
        if (questions.Count == 0)
        {
            return false;
        }

        var orderedIndexes = questions
            .Select(question => question.OrderIndex)
            .OrderBy(index => index)
            .ToArray();

        for (var expectedIndex = 0; expectedIndex < orderedIndexes.Length; expectedIndex++)
        {
            if (orderedIndexes[expectedIndex] != expectedIndex)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyDictionary<string, string[]>? ValidateAddQuestionRequest(AddQuizQuestionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateQuestionFields(
            request.Text,
            request.TimeLimitSec,
            request.QuestionType,
            request.CorrectOption,
            request.CorrectNumericValue,
            request.OptionA,
            request.OptionB,
            request.OptionC,
            request.OptionD,
            errors,
            nameof(AddQuizQuestionRequest.Text),
            nameof(AddQuizQuestionRequest.TimeLimitSec),
            nameof(AddQuizQuestionRequest.QuestionType),
            nameof(AddQuizQuestionRequest.CorrectOption),
            nameof(AddQuizQuestionRequest.CorrectNumericValue),
            nameof(AddQuizQuestionRequest.OptionA),
            nameof(AddQuizQuestionRequest.OptionB),
            nameof(AddQuizQuestionRequest.OptionC),
            nameof(AddQuizQuestionRequest.OptionD));

        if (request.Order.HasValue && request.Order.Value < 1)
        {
            errors[nameof(AddQuizQuestionRequest.Order)] = ["Pořadí otázky musí být alespoň 1."];
        }

        return errors.Count == 0 ? null : errors;
    }

    private static IReadOnlyDictionary<string, string[]>? ValidateUpdateQuestionRequest(UpdateQuizQuestionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateQuestionFields(
            request.Text,
            request.TimeLimitSec,
            request.QuestionType,
            request.CorrectOption,
            request.CorrectNumericValue,
            request.OptionA,
            request.OptionB,
            request.OptionC,
            request.OptionD,
            errors,
            nameof(UpdateQuizQuestionRequest.Text),
            nameof(UpdateQuizQuestionRequest.TimeLimitSec),
            nameof(UpdateQuizQuestionRequest.QuestionType),
            nameof(UpdateQuizQuestionRequest.CorrectOption),
            nameof(UpdateQuizQuestionRequest.CorrectNumericValue),
            nameof(UpdateQuizQuestionRequest.OptionA),
            nameof(UpdateQuizQuestionRequest.OptionB),
            nameof(UpdateQuizQuestionRequest.OptionC),
            nameof(UpdateQuizQuestionRequest.OptionD));

        if (request.Order < 1)
        {
            errors[nameof(UpdateQuizQuestionRequest.Order)] = ["Pořadí otázky musí být alespoň 1."];
        }

        return errors.Count == 0 ? null : errors;
    }

    private static void ValidateQuestionFields(
        string text,
        int timeLimitSec,
        QuestionType questionType,
        OptionKey? correctOption,
        decimal? correctNumericValue,
        string? optionA,
        string? optionB,
        string? optionC,
        string? optionD,
        IDictionary<string, string[]> errors,
        string textFieldName,
        string timeLimitFieldName,
        string questionTypeFieldName,
        string correctOptionFieldName,
        string correctNumericValueFieldName,
        string optionAFieldName,
        string optionBFieldName,
        string optionCFieldName,
        string optionDFieldName)
    {
        var sanitizedQuestionText = TextInputSanitizer.SanitizeSingleLine(text);

        if (string.IsNullOrWhiteSpace(sanitizedQuestionText))
        {
            errors[textFieldName] = ["Text otázky je povinný."];
        }
        else if (sanitizedQuestionText.Length > MaxQuestionTextLength)
        {
            errors[textFieldName] = [$"Text otázky může mít maximálně {MaxQuestionTextLength} znaků."];
        }

        if (timeLimitSec is < 10 or > 300)
        {
            errors[timeLimitFieldName] = ["Časový limit otázky musí být v rozsahu 10 až 300 sekund."];
        }

        if (questionType == QuestionType.MultipleChoice)
        {
            ValidateQuestionOption(optionA, optionAFieldName, "Odpověď A je povinná.", errors);
            ValidateQuestionOption(optionB, optionBFieldName, "Odpověď B je povinná.", errors);
            ValidateQuestionOption(optionC, optionCFieldName, "Odpověď C je povinná.", errors);
            ValidateQuestionOption(optionD, optionDFieldName, "Odpověď D je povinná.", errors);

            if (correctOption is null)
            {
                errors[correctOptionFieldName] = ["Správná odpověď je povinná pro typ otázky Výběr A-D."];
            }
        }
        else if (questionType == QuestionType.NumericClosest)
        {
            if (correctNumericValue is null)
            {
                errors[correctNumericValueFieldName] = ["Správná číselná hodnota je povinná pro číselný typ otázky."];
            }
        }
        else
        {
            errors[questionTypeFieldName] = ["Neplatný typ otázky."];
        }
    }

    private static void ValidateQuestionOption(
        string? optionValue,
        string optionName,
        string requiredErrorMessage,
        IDictionary<string, string[]> errors)
    {
        var sanitizedValue = TextInputSanitizer.SanitizeSingleLine(optionValue);
        if (string.IsNullOrWhiteSpace(sanitizedValue))
        {
            errors[optionName] = [requiredErrorMessage];
            return;
        }

        if (sanitizedValue.Length > MaxQuestionOptionLength)
        {
            errors[optionName] = [$"Text odpovědi může mít maximálně {MaxQuestionOptionLength} znaků."];
        }
    }

    private static IReadOnlyDictionary<string, string[]>? ValidateCreateSessionRequest(CreateSessionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var normalizedJoinCode = request.JoinCode.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedJoinCode))
        {
            errors[nameof(CreateSessionRequest.JoinCode)] = ["Join kód je povinný."];
        }
        else
        {
            if (normalizedJoinCode.Length < MinJoinCodeLength)
            {
                errors[nameof(CreateSessionRequest.JoinCode)] = [$"Join kód musí mít alespoň {MinJoinCodeLength} znaky."];
            }
        }

        return errors.Count == 0 ? null : errors;
    }

    private static string GenerateOrganizerToken()
    {
        Span<byte> tokenBytes = stackalloc byte[OrganizerTokenEntropyBytes];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToHexString(tokenBytes);
    }

    private static string HashOrganizerToken(string organizerToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(organizerToken));
        return Convert.ToHexString(hashBytes);
    }

    private static bool VerifyOrganizerToken(string organizerToken, string organizerTokenHash)
    {
        if (string.IsNullOrWhiteSpace(organizerToken) || string.IsNullOrWhiteSpace(organizerTokenHash))
        {
            return false;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(organizerTokenHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(organizerToken.Trim()));

        return expectedHash.Length == actualHash.Length && CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static bool TryAuthorizeOrganizer(Quiz quiz, string? organizerToken, string? organizerPassword, out ApiErrorResponse? error)
    {
        if (string.IsNullOrWhiteSpace(organizerToken) && string.IsNullOrWhiteSpace(organizerPassword))
        {
            error = new ApiErrorResponse(ApiErrorCode.MissingAuthToken, "Chybí organizátorská autentizace (X-Organizer-Token nebo X-Quiz-Password).");
            return false;
        }

        var tokenMatches = VerifyOrganizerToken(organizerToken ?? string.Empty, quiz.QuizOrganizerTokenHash);
        var passwordMatches = VerifyPassword(organizerPassword, quiz.DeletePasswordHash);

        if (tokenMatches || passwordMatches)
        {
            error = null;
            return true;
        }

        error = new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatné Administrátorké heslo kvízu.");
        return false;
    }

    private static string HashPassword(string password)
    {
        Span<byte> saltBytes = stackalloc byte[PasswordSaltBytes];
        RandomNumberGenerator.Fill(saltBytes);

        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password.Trim(),
            saltBytes,
            PasswordHashIterations,
            HashAlgorithmName.SHA256,
            PasswordHashBytes);

        return ComposePasswordHash(saltBytes, hashBytes);
    }

    private static string ComposePasswordHash(ReadOnlySpan<byte> saltBytes, ReadOnlySpan<byte> hashBytes)
    {
        return string.Concat(
            "pbkdf2-sha256$",
            PasswordHashIterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "$",
            Convert.ToHexString(saltBytes),
            "$",
            Convert.ToHexString(hashBytes));
    }

    private static bool VerifyPassword(string? password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromHexString(parts[2]);
            expectedHash = Convert.FromHexString(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password.Trim(),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return expectedHash.Length == actualHash.Length && CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private sealed record QuizCreatedAuditPayload(Guid QuizId, string QuizName);

    private sealed record QuizImportedAuditPayload(Guid QuizId, int ImportedQuestionsCount);

    private sealed record SessionCreatedAuditPayload(Guid SessionId, Guid QuizId, string JoinCode);

    private sealed record QuestionAddedAuditPayload(Guid QuizId, Guid QuestionId, int OrderIndex, QuestionType QuestionType);

    private sealed record QuestionUpdatedAuditPayload(Guid QuizId, Guid QuestionId, int OrderIndex, QuestionType QuestionType);

    private sealed record QuestionDeletedAuditPayload(Guid QuizId, Guid QuestionId, int OrderIndex);

    private sealed record QuizStartPermissionUpdatedAuditPayload(Guid QuizId, bool IsStartAllowedForEveryone);

    private sealed record QuizDeletedAuditPayload(Guid QuizId);
}

public sealed record CreateQuizOperationResult(
    CreateQuizResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static CreateQuizOperationResult Success(CreateQuizResponse response) => new(response, null);

    public static CreateQuizOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record ImportQuizCsvOperationResult(
    ImportQuizCsvResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static ImportQuizCsvOperationResult Success(ImportQuizCsvResponse response) => new(response, null);

    public static ImportQuizCsvOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record CreateSessionOperationResult(
    CreateSessionResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static CreateSessionOperationResult Success(CreateSessionResponse response) => new(response, null);

    public static CreateSessionOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record GenerateJoinCodeOperationResult(
    GenerateJoinCodeResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static GenerateJoinCodeOperationResult Success(GenerateJoinCodeResponse response) => new(response, null);

    public static GenerateJoinCodeOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record AddQuizQuestionOperationResult(
    AddQuizQuestionResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static AddQuizQuestionOperationResult Success(AddQuizQuestionResponse response) => new(response, null);

    public static AddQuizQuestionOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record QuizDetailOperationResult(
    QuizDetailResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static QuizDetailOperationResult Success(QuizDetailResponse response) => new(response, null);

    public static QuizDetailOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record DeleteQuizQuestionOperationResult(
    bool IsSuccess,
    ApiErrorResponse? Error)
{
    public static DeleteQuizQuestionOperationResult Success() => new(true, null);

    public static DeleteQuizQuestionOperationResult Fail(ApiErrorResponse error) => new(false, error);
}

public sealed record ReorderQuizQuestionOperationResult(
    bool IsSuccess,
    ApiErrorResponse? Error)
{
    public static ReorderQuizQuestionOperationResult Success() => new(true, null);

    public static ReorderQuizQuestionOperationResult Fail(ApiErrorResponse error) => new(false, error);
}

public sealed record DeleteQuizOperationResult(
    bool IsSuccess,
    ApiErrorResponse? Error)
{
    public static DeleteQuizOperationResult Success() => new(true, null);

    public static DeleteQuizOperationResult Fail(ApiErrorResponse error) => new(false, error);
}

public sealed record UpdateQuizStartPermissionOperationResult(
    UpdateQuizStartPermissionResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static UpdateQuizStartPermissionOperationResult Success(UpdateQuizStartPermissionResponse response) => new(response, null);

    public static UpdateQuizStartPermissionOperationResult Fail(ApiErrorResponse error) => new(null, error);
}
