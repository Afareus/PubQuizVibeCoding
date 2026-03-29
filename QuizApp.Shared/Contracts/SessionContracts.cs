using QuizApp.Shared.Enums;

namespace QuizApp.Shared.Contracts;

public sealed record CreateSessionRequest(
    string JoinCode);

public sealed record CreateSessionResponse(
    Guid SessionId,
    string JoinCode,
    SessionStatus Status);

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
    OptionKey SelectedOption);

public sealed record SubmitAnswerResponse(
    Guid SessionId,
    Guid TeamId,
    Guid QuestionId,
    OptionKey SelectedOption,
    DateTimeOffset SubmittedAtUtc);

public sealed record SessionStateSnapshotResponse(
    Guid SessionId,
    string QuizName,
    SessionStatus Status,
    int? CurrentQuestionIndex,
    DateTimeOffset? CurrentQuestionStartedAtUtc,
    DateTimeOffset? QuestionDeadlineUtc,
    SnapshotQuestionDto? CurrentQuestion,
    IReadOnlyList<SnapshotTeamDto> Teams);

public sealed record OrganizerSessionSnapshotResponse(
    Guid SessionId,
    Guid QuizId,
    string JoinCode,
    SessionStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    int? CurrentQuestionIndex,
    DateTimeOffset? CurrentQuestionStartedAtUtc,
    DateTimeOffset? QuestionDeadlineUtc,
    SnapshotQuestionDto? CurrentQuestion,
    IReadOnlyList<SnapshotTeamDto> Teams);

public sealed record SnapshotQuestionDto(
    Guid QuestionId,
    string Text,
    int TimeLimitSec,
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

public sealed record CorrectAnswerDto(
    Guid QuestionId,
    int OrderIndex,
    string QuestionText,
    OptionKey CorrectOption,
    IReadOnlyList<SnapshotQuestionOptionDto> Options);
