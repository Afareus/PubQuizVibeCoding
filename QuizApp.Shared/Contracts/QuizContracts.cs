using QuizApp.Shared.Enums;

namespace QuizApp.Shared.Contracts;

public sealed record CreateQuizRequest(
    string Name,
    string DeletePassword);

public sealed record CreateQuizResponse(
    Guid QuizId,
    string QuizOrganizerToken);

public sealed record QuizListItemResponse(
    Guid QuizId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    bool IsStartAllowedForEveryone);

public sealed record ImportQuizCsvRequest(
    Guid QuizId,
    string CsvContent);

public sealed record ImportQuizCsvResponse(
    int ImportedQuestionsCount,
    IReadOnlyList<CsvValidationIssueDto> ValidationIssues);

public sealed record AddQuizQuestionRequest(
    string Text,
    int TimeLimitSec,
    QuestionType QuestionType,
    OptionKey? CorrectOption,
    decimal? CorrectNumericValue,
    string? OptionA,
    string? OptionB,
    string? OptionC,
    string? OptionD,
    int? Order = null);

public sealed record UpdateQuizQuestionRequest(
    string Text,
    int TimeLimitSec,
    QuestionType QuestionType,
    OptionKey? CorrectOption,
    decimal? CorrectNumericValue,
    string? OptionA,
    string? OptionB,
    string? OptionC,
    string? OptionD);

public sealed record ReorderQuizQuestionRequest(
    Guid QuestionId,
    int Direction);

public sealed record AddQuizQuestionResponse(
    Guid QuestionId,
    int OrderIndex,
    QuestionType QuestionType);

public sealed record QuizDetailResponse(
    Guid QuizId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    int QuestionCount,
    bool IsStartAllowedForEveryone,
    IReadOnlyList<QuizDetailQuestionDto> Questions);

public sealed record UpdateQuizStartPermissionRequest(
    bool IsStartAllowedForEveryone);

public sealed record UpdateQuizStartPermissionResponse(
    Guid QuizId,
    bool IsStartAllowedForEveryone);

public sealed record QuizDetailQuestionDto(
    Guid QuestionId,
    int OrderIndex,
    string Text,
    int TimeLimitSec,
    QuestionType QuestionType,
    OptionKey? CorrectOption,
    decimal? CorrectNumericValue,
    IReadOnlyList<QuizDetailQuestionOptionDto> Options);

public sealed record QuizDetailQuestionOptionDto(
    OptionKey OptionKey,
    string Text);

public sealed record CsvValidationIssueDto(
    int Row,
    string Column,
    ApiErrorCode Code,
    string Message);

public sealed record ApiErrorResponse(
    ApiErrorCode Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
