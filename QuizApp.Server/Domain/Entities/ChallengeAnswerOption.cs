namespace QuizApp.Server.Domain.Entities;

public sealed class ChallengeAnswerOption
{
    private ChallengeAnswerOption()
    {
    }

    public ChallengeAnswerOption(Guid challengeAnswerOptionId, Guid challengeQuestionId, string optionKey, string text)
    {
        if (challengeAnswerOptionId == Guid.Empty)
            throw new ArgumentException("ChallengeAnswerOption id must not be empty.", nameof(challengeAnswerOptionId));
        if (challengeQuestionId == Guid.Empty)
            throw new ArgumentException("ChallengeQuestion id must not be empty.", nameof(challengeQuestionId));

        ChallengeAnswerOptionId = challengeAnswerOptionId;
        ChallengeQuestionId = challengeQuestionId;
        OptionKey = EntityGuards.Required(optionKey, nameof(optionKey), "Option key is required.");
        Text = EntityGuards.Required(text, nameof(text), "Option text is required.");
    }

    public Guid ChallengeAnswerOptionId { get; private set; }

    public Guid ChallengeQuestionId { get; private set; }

    public string OptionKey { get; private set; } = string.Empty;

    public string Text { get; private set; } = string.Empty;

    public ChallengeQuestion Question { get; private set; } = null!;
}
