namespace QuizApp.Server.Domain.Entities;

public sealed class ChallengeQuestion
{
    private readonly List<ChallengeAnswerOption> _options = [];

    private ChallengeQuestion()
    {
    }

    public ChallengeQuestion(Guid challengeQuestionId, Guid challengeId, int orderIndex, string text, string creatorSelectedOptionKey)
    {
        if (challengeQuestionId == Guid.Empty)
            throw new ArgumentException("ChallengeQuestion id must not be empty.", nameof(challengeQuestionId));
        if (challengeId == Guid.Empty)
            throw new ArgumentException("Challenge id must not be empty.", nameof(challengeId));

        ChallengeQuestionId = challengeQuestionId;
        ChallengeId = challengeId;
        OrderIndex = orderIndex;
        Text = EntityGuards.Required(text, nameof(text), "Question text is required.");
        CreatorSelectedOptionKey = EntityGuards.Required(creatorSelectedOptionKey, nameof(creatorSelectedOptionKey), "Creator selected option key is required.");
    }

    public Guid ChallengeQuestionId { get; private set; }

    public Guid ChallengeId { get; private set; }

    public int OrderIndex { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public string CreatorSelectedOptionKey { get; private set; } = string.Empty;

    public Challenge Challenge { get; private set; } = null!;

    public IReadOnlyList<ChallengeAnswerOption> Options => _options.AsReadOnly();
}
