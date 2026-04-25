namespace QuizApp.Shared.Contracts;

// --- Template ---

public sealed record ChallengeTemplateQuestionDto(
    int TemplateQuestionId,
    int OrderIndex,
    string Text,
    IReadOnlyList<ChallengeTemplateOptionDto> Options);

public sealed record ChallengeTemplateOptionDto(
    string OptionKey,
    string Text);

public sealed record GetChallengeTemplateResponse(
    IReadOnlyList<ChallengeTemplateQuestionDto> Questions);

// --- Create ---

public sealed record CreateChallengeAnswerDto(
    int TemplateQuestionId,
    string SelectedOptionKey);

public sealed record CreateChallengeRequest(
    string CreatorName,
    string Title,
    IReadOnlyList<CreateChallengeAnswerDto> Answers);

public sealed record CreateChallengeResponse(
    Guid ChallengeId,
    string PublicCode,
    string Title,
    string CreatorName);

// --- Play ---

public sealed record ChallengeQuestionDto(
    Guid QuestionId,
    int OrderIndex,
    string Text,
    IReadOnlyList<ChallengeOptionDto> Options);

public sealed record ChallengeOptionDto(
    string OptionKey,
    string Text);

public sealed record GetChallengeResponse(
    string PublicCode,
    string Title,
    string CreatorName,
    IReadOnlyList<ChallengeQuestionDto> Questions);

// --- Submit ---

public sealed record SubmitChallengeAnswerDto(
    Guid QuestionId,
    string SelectedOptionKey);

public sealed record SubmitChallengeAnswersRequest(
    string ParticipantName,
    IReadOnlyList<SubmitChallengeAnswerDto> Answers);

public sealed record ChallengeLeaderboardEntryDto(
    int Rank,
    string ParticipantName,
    int Score,
    int MaxScore,
    DateTimeOffset SubmittedAtUtc);

public sealed record SubmitChallengeAnswersResponse(
    Guid SubmissionId,
    int Score,
    int MaxScore,
    int Rank,
    IReadOnlyList<ChallengeLeaderboardEntryDto> Leaderboard);

// --- Leaderboard ---

public sealed record ChallengeLeaderboardResponse(
    string PublicCode,
    string Title,
    string CreatorName,
    IReadOnlyList<ChallengeLeaderboardEntryDto> Entries);

// --- Submission result ---

public sealed record ChallengeSubmissionResultAnswerDto(
    Guid QuestionId,
    int OrderIndex,
    string QuestionText,
    string SelectedOptionKey,
    string CorrectOptionKey,
    bool IsCorrect);

public sealed record GetChallengeSubmissionResultResponse(
    Guid SubmissionId,
    string ParticipantName,
    int Score,
    int MaxScore,
    int Rank,
    IReadOnlyList<ChallengeSubmissionResultAnswerDto> Answers,
    IReadOnlyList<ChallengeLeaderboardEntryDto> Leaderboard);
