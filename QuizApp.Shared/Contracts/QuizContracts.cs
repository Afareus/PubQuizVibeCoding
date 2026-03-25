using QuizApp.Shared.Enums;

namespace QuizApp.Shared.Contracts;

public sealed record CreateQuizRequest(
    string Name,
    string DeletePassword);

public sealed record CreateQuizResponse(
    Guid QuizId,
    string QuizOrganizerToken);

public sealed record ImportQuizCsvRequest(
    Guid QuizId,
    string CsvContent);

public sealed record ImportQuizCsvResponse(
    int ImportedQuestionsCount,
    IReadOnlyList<CsvValidationIssueDto> ValidationIssues);

public sealed record CsvValidationIssueDto(
    int Row,
    string Column,
    ApiErrorCode Code,
    string Message);

public sealed record ApiErrorResponse(
    ApiErrorCode Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
