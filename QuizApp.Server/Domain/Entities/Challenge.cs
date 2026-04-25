namespace QuizApp.Server.Domain.Entities;

public sealed class Challenge
{
    private readonly List<ChallengeQuestion> _questions = [];
    private readonly List<ChallengeSubmission> _submissions = [];

    private Challenge()
    {
    }

    public Challenge(Guid challengeId, string publicCode, string title, string creatorName, DateTime createdAtUtc)
    {
        if (challengeId == Guid.Empty)
            throw new ArgumentException("Challenge id must not be empty.", nameof(challengeId));

        ChallengeId = challengeId;
        PublicCode = EntityGuards.Required(publicCode, nameof(publicCode), "Public code is required.");
        Title = EntityGuards.Required(title, nameof(title), "Title is required.");
        CreatorName = EntityGuards.Required(creatorName, nameof(creatorName), "Creator name is required.");
        CreatedAtUtc = EntityGuards.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid ChallengeId { get; private set; }

    public string PublicCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string CreatorName { get; private set; } = string.Empty;

    public string? CreatorTokenHash { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public IReadOnlyList<ChallengeQuestion> Questions => _questions.AsReadOnly();

    public IReadOnlyList<ChallengeSubmission> Submissions => _submissions.AsReadOnly();
}
