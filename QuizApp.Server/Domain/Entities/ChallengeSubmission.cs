namespace QuizApp.Server.Domain.Entities;

public sealed class ChallengeSubmission
{
    private readonly List<ChallengeSubmissionAnswer> _answers = [];

    private ChallengeSubmission()
    {
    }

    public ChallengeSubmission(Guid challengeSubmissionId, Guid challengeId, string participantName, int score, int maxScore, DateTime submittedAtUtc)
    {
        if (challengeSubmissionId == Guid.Empty)
            throw new ArgumentException("ChallengeSubmission id must not be empty.", nameof(challengeSubmissionId));
        if (challengeId == Guid.Empty)
            throw new ArgumentException("Challenge id must not be empty.", nameof(challengeId));

        ChallengeSubmissionId = challengeSubmissionId;
        ChallengeId = challengeId;
        ParticipantName = EntityGuards.Required(participantName, nameof(participantName), "Participant name is required.");
        Score = score;
        MaxScore = maxScore;
        SubmittedAtUtc = EntityGuards.Utc(submittedAtUtc, nameof(submittedAtUtc));
    }

    public Guid ChallengeSubmissionId { get; private set; }

    public Guid ChallengeId { get; private set; }

    public string ParticipantName { get; private set; } = string.Empty;

    public int Score { get; private set; }

    public int MaxScore { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public Challenge Challenge { get; private set; } = null!;

    public IReadOnlyList<ChallengeSubmissionAnswer> Answers => _answers.AsReadOnly();
}
