using QuizApp.Shared.Enums;

namespace QuizApp.Server.Domain.Entities;

public sealed class TeamAnswer
{
    private TeamAnswer()
    {
    }

    private TeamAnswer(
        Guid teamAnswerId,
        Guid sessionId,
        Guid teamId,
        Guid questionId,
        OptionKey selectedOption,
        DateTime submittedAtUtc,
        bool isCorrect,
        long responseTimeMs)
    {
        if (teamAnswerId == Guid.Empty)
        {
            throw new ArgumentException("Team answer id must not be empty.", nameof(teamAnswerId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session id must not be empty.", nameof(sessionId));
        }

        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Team id must not be empty.", nameof(teamId));
        }

        if (questionId == Guid.Empty)
        {
            throw new ArgumentException("Question id must not be empty.", nameof(questionId));
        }

        TeamAnswerId = teamAnswerId;
        SessionId = sessionId;
        TeamId = teamId;
        QuestionId = questionId;
        SelectedOption = selectedOption;
        SubmittedAtUtc = EntityGuards.Utc(submittedAtUtc, nameof(submittedAtUtc));
        IsCorrect = isCorrect;
        ResponseTimeMs = EntityGuards.NonNegative(responseTimeMs, nameof(responseTimeMs), "Response time must be non-negative.");
    }

    public Guid TeamAnswerId { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid QuestionId { get; private set; }

    public OptionKey SelectedOption { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public bool IsCorrect { get; private set; }

    public long ResponseTimeMs { get; private set; }

    public QuizSession? Session { get; private set; }

    public Team? Team { get; private set; }

    public Question? Question { get; private set; }

    public static TeamAnswer Create(
        Guid teamAnswerId,
        Guid sessionId,
        Guid teamId,
        Guid questionId,
        OptionKey selectedOption,
        DateTime submittedAtUtc,
        bool isCorrect,
        long responseTimeMs)
    {
        return new TeamAnswer(teamAnswerId, sessionId, teamId, questionId, selectedOption, submittedAtUtc, isCorrect, responseTimeMs);
    }
}