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

    Task<ImportQuizCsvOperationResult> ImportQuizCsvAsync(Guid quizId, string organizerToken, string csvContent, CancellationToken cancellationToken);
}

public sealed class QuizManagementService : IQuizManagementService
{
    private const int OrganizerTokenEntropyBytes = 32;
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

    public async Task<ImportQuizCsvOperationResult> ImportQuizCsvAsync(Guid quizId, string organizerToken, string csvContent, CancellationToken cancellationToken)
    {
        var quiz = await _dbContext.Quizzes
            .Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

        if (quiz is null)
        {
            return ImportQuizCsvOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.ResourceNotFound, "Kvíz nebyl nalezen."));
        }

        if (!VerifyOrganizerToken(organizerToken, quiz.QuizOrganizerTokenHash))
        {
            return ImportQuizCsvOperationResult.Fail(new ApiErrorResponse(ApiErrorCode.InvalidAuthToken, "Neplatný organizer token."));
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

    private sealed record QuizCreatedAuditPayload(Guid QuizId, string QuizName);

    private sealed record QuizImportedAuditPayload(Guid QuizId, int ImportedQuestionsCount);
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
