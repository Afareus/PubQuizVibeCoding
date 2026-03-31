namespace QuizApp.Server.Domain.Entities;

public sealed class SessionResult
{
    private SessionResult()
    {
    }

    private SessionResult(
        Guid sessionResultId,
        Guid sessionId,
        Guid teamId,
        int score,
        int correctCount,
        long totalCorrectResponseTimeMs,
        int rank)
    {
        if (sessionResultId == Guid.Empty)
        {
            throw new ArgumentException("Session result id must not be empty.", nameof(sessionResultId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session id must not be empty.", nameof(sessionId));
        }

        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Team id must not be empty.", nameof(teamId));
        }

        SessionResultId = sessionResultId;
        SessionId = sessionId;
        TeamId = teamId;
        Score = EntityGuards.Range(score, 0, int.MaxValue, nameof(score), "Score must be non-negative.");
        CorrectCount = EntityGuards.Range(correctCount, 0, int.MaxValue, nameof(correctCount), "Correct count must be non-negative.");
        TotalCorrectResponseTimeMs = EntityGuards.NonNegative(totalCorrectResponseTimeMs, nameof(totalCorrectResponseTimeMs), "Total response time must be non-negative.");
        Rank = EntityGuards.Range(rank, 1, int.MaxValue, nameof(rank), "Rank must be at least 1.");
    }

    public Guid SessionResultId { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid TeamId { get; private set; }

    public int Score { get; private set; }

    public int CorrectCount { get; private set; }

    public long TotalCorrectResponseTimeMs { get; private set; }

    public int Rank { get; private set; }

    public QuizSession? Session { get; private set; }

    public Team? Team { get; private set; }

    public static SessionResult Create(
        Guid sessionResultId,
        Guid sessionId,
        Guid teamId,
        int score,
        int correctCount,
        long totalCorrectResponseTimeMs,
        int rank)
    {
        return new SessionResult(sessionResultId, sessionId, teamId, score, correctCount, totalCorrectResponseTimeMs, rank);
    }
}