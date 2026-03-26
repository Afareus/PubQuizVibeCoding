using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuizApp.Server.Application.QuizImport;
using QuizApp.Server.Domain.Entities;
using QuizApp.Server.Persistence;
using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Server.Application.Quizzes;

public interface IQuizManagementService
{
    Task<CreateQuizOperationResult> CreateQuizAsync(CreateQuizRequest request, CancellationToken cancellationToken);

    Task<CreateSessionOperationResult> CreateSessionAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<ImportQuizCsvOperationResult> ImportQuizCsvAsync(Guid quizId, string? organizerToken, string? organizerPassword, string csvContent, CancellationToken cancellationToken);

    Task<QuizDetailOperationResult> GetQuizDetailAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);

    Task<DeleteQuizOperationResult> DeleteQuizAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken);
}

public sealed class QuizManagementService : IQuizManagementService
{
    private const int OrganizerTokenEntropyBytes = 32;
    private const string JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int JoinCodeLength = 8;
    private const int MaxJoinCodeGenerationAttempts = 10;
    private const int PasswordSaltBytes = 16;
    private const int PasswordHashBytes = 32;
    private const int PasswordHashIterations = 100_000;

    private readonly QuizAppDbContext _dbContext;
    private readonly IQuizCsvParser _quizCsvParser;

    public QuizManagementService(QuizAppDbContext dbContext, IQuizCsvParser quizCsvParser)
    {
        _dbContext = dbContext;
        _quizCsvParser = quizCsvParser;
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

        var quiz = Quiz.Create(
            quizId,
            request.Name.Trim(),
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

    public async Task<CreateSessionOperationResult> CreateSessionAsync(Guid quizId, string? organizerToken, string? organizerPassword, CancellationToken cancellationToken)
    {
        var quiz = await _dbContext.Quizzes
            .Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return CreateSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (!TryAuthorizeOrganizer(quiz, organizerToken, organizerPassword, out var authError))
        {
            return CreateSessionOperationResult.Fail(authError!);
        }

        if (quiz.Questions.Count == 0)
        {
            return CreateSessionOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.QuizHasNoQuestions, "Session lze založit jen nad kvízem, který obsahuje alespoň jednu otázku."));
        }

        var nowUtc = DateTime.UtcNow;
        var joinCode = await GenerateUniqueJoinCodeAsync(cancellationToken);
        var session = QuizSession.Create(Guid.NewGuid(), quiz.QuizId, joinCode, nowUtc);

        _dbContext.Sessions.Add(session);
        _dbContext.AuditLogs.Add(AuditLog.Create(
            Guid.NewGuid(),
            nowUtc,
            "SESSION_CREATED",
            quiz.QuizId,
            session.SessionId,
            JsonSerializer.Serialize(new SessionCreatedAuditPayload(session.SessionId, quiz.QuizId, session.JoinCode))));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreateSessionOperationResult.Success(new CreateSessionResponse(session.SessionId, session.JoinCode, session.Status));
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
                parsedQuestion.CorrectOption));

            options.Add(QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.A, parsedQuestion.OptionA));
            options.Add(QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.B, parsedQuestion.OptionB));
            options.Add(QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.C, parsedQuestion.OptionC));
            options.Add(QuestionOption.Create(Guid.NewGuid(), questionId, OptionKey.D, parsedQuestion.OptionD));
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
                question.CorrectOption,
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
            questions));
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
            return DeleteQuizOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.MissingAuthToken, "Chybí mazací heslo v hlavičce X-Quiz-Password."));
        }

        if (!TryAuthorizeOrganizer(quiz, organizerToken, organizerPassword, out var authError))
        {
            return DeleteQuizOperationResult.Fail(authError!);
        }

        if (!VerifyPassword(organizerPassword, quiz.DeletePasswordHash))
        {
            return DeleteQuizOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatné mazací heslo."));
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

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(CreateQuizRequest.Name)] = ["Název kvízu je povinný."];
        }

        if (string.IsNullOrWhiteSpace(request.DeletePassword))
        {
            errors[nameof(CreateQuizRequest.DeletePassword)] = ["Mazací heslo je povinné."];
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

        error = new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatný organizer token nebo mazací heslo.");
        return false;
    }

    private async Task<string> GenerateUniqueJoinCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxJoinCodeGenerationAttempts; attempt++)
        {
            var joinCode = GenerateJoinCode();
            var exists = await _dbContext.Sessions.AnyAsync(x => x.JoinCode == joinCode, cancellationToken);
            if (!exists)
            {
                return joinCode;
            }
        }

        throw new InvalidOperationException("Nepodařilo se vygenerovat unikátní join code.");
    }

    private static string GenerateJoinCode()
    {
        Span<char> chars = stackalloc char[JoinCodeLength];

        for (var i = 0; i < chars.Length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(JoinCodeAlphabet.Length);
            chars[i] = JoinCodeAlphabet[index];
        }

        return new string(chars);
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

public sealed record QuizDetailOperationResult(
    QuizDetailResponse? Response,
    ApiErrorResponse? Error)
{
    public bool IsSuccess => Error is null;

    public static QuizDetailOperationResult Success(QuizDetailResponse response) => new(response, null);

    public static QuizDetailOperationResult Fail(ApiErrorResponse error) => new(null, error);
}

public sealed record DeleteQuizOperationResult(
    bool IsSuccess,
    ApiErrorResponse? Error)
{
    public static DeleteQuizOperationResult Success() => new(true, null);

    public static DeleteQuizOperationResult Fail(ApiErrorResponse error) => new(false, error);
}
