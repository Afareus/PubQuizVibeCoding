using QuizApp.Shared.Enums;

namespace QuizApp.Shared.Contracts;

public sealed record CreateSessionRequest(
    Guid QuizId);

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

public sealed record SessionStateSnapshotResponse(
    Guid SessionId,
    SessionStatus Status,
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
