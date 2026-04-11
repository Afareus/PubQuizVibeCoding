using QuizApp.Shared.Enums;

namespace QuizApp.Shared.Contracts;

public sealed record CreateSessionRequest(
    string JoinCode);

public sealed record CreateSessionResponse(
    Guid SessionId,
    string JoinCode,
    SessionStatus Status);

public sealed record GenerateJoinCodeResponse(
    string JoinCode);

public sealed record JoinSessionRequest(
    string JoinCode,
    string TeamName);

public sealed record JoinSessionResponse(
    Guid SessionId,
    Guid TeamId,
    string TeamReconnectToken,
    SessionStatus Status);

public sealed record CancelSessionRequest(
    bool ConfirmCancellation);

public sealed record SubmitAnswerRequest(
    Guid TeamId,
    Guid QuestionId,
    OptionKey? SelectedOption,
    decimal? NumericValue);

public sealed record SubmitAnswerResponse(
    Guid SessionId,
    Guid TeamId,
    Guid QuestionId,
    OptionKey? SelectedOption,
    decimal? NumericValue,
    DateTimeOffset SubmittedAtUtc);

public sealed record SessionStateSnapshotResponse(
    Guid SessionId,
    string QuizName,
    SessionStatus Status,
    int? CurrentQuestionIndex,
    DateTimeOffset? CurrentQuestionStartedAtUtc,
    DateTimeOffset? QuestionDeadlineUtc,
    SnapshotQuestionDto? CurrentQuestion,
    IReadOnlyList<SnapshotTeamDto> Teams,
    bool ResultsPublished,
    bool IsCurrentQuestionAnsweringClosed);

public sealed record OrganizerSessionSnapshotResponse(
    Guid SessionId,
    Guid QuizId,
    string QuizName,
    string JoinCode,
    SessionStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    int? CurrentQuestionIndex,
    int TotalQuestionCount,
    DateTimeOffset? CurrentQuestionStartedAtUtc,
    DateTimeOffset? QuestionDeadlineUtc,
    SnapshotQuestionDto? CurrentQuestion,
    IReadOnlyList<SnapshotTeamDto> Teams,
    bool ResultsPublished);

public sealed record SnapshotQuestionDto(
    Guid QuestionId,
    string Text,
    int TimeLimitSec,
    QuestionType QuestionType,
    IReadOnlyList<SnapshotQuestionOptionDto> Options);

public sealed record SnapshotQuestionOptionDto(
    OptionKey OptionKey,
    string Text);

public sealed record SnapshotTeamDto(
    Guid TeamId,
    string TeamName);

public sealed record SessionResultsResponse(
    Guid SessionId,
    SessionStatus Status,
    IReadOnlyList<SessionResultDto> Results);

public sealed record SessionResultDto(
    Guid TeamId,
    string TeamName,
    int Score,
    int CorrectCount,
    long TotalCorrectResponseTimeMs,
    int Rank);

public sealed record CorrectAnswersResponse(
    Guid SessionId,
    IReadOnlyList<CorrectAnswerDto> CorrectAnswers);

public sealed record CurrentQuestionCorrectAnswerResponse(
    Guid SessionId,
    Guid QuestionId,
    QuestionType QuestionType,
    OptionKey? CorrectOption,
    decimal? CorrectNumericValue);

public sealed record CorrectAnswerDto(
    Guid QuestionId,
    int OrderIndex,
    string QuestionText,
    QuestionType QuestionType,
    OptionKey? CorrectOption,
    decimal? CorrectNumericValue,
    OptionKey? TeamSelectedOption,
    decimal? TeamSubmittedNumericValue,
    IReadOnlyList<SnapshotQuestionOptionDto> Options);
