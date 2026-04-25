namespace QuizApp.Server.Domain.Entities;

public sealed class ChallengeSubmissionAnswer
{
    private ChallengeSubmissionAnswer()
    {
    }

    public ChallengeSubmissionAnswer(Guid challengeSubmissionAnswerId, Guid challengeSubmissionId, Guid challengeQuestionId, string selectedOptionKey, bool isCorrect)
    {
        if (challengeSubmissionAnswerId == Guid.Empty)
            throw new ArgumentException("ChallengeSubmissionAnswer id must not be empty.", nameof(challengeSubmissionAnswerId));
        if (challengeSubmissionId == Guid.Empty)
            throw new ArgumentException("ChallengeSubmission id must not be empty.", nameof(challengeSubmissionId));
        if (challengeQuestionId == Guid.Empty)
            throw new ArgumentException("ChallengeQuestion id must not be empty.", nameof(challengeQuestionId));

        ChallengeSubmissionAnswerId = challengeSubmissionAnswerId;
        ChallengeSubmissionId = challengeSubmissionId;
        ChallengeQuestionId = challengeQuestionId;
        SelectedOptionKey = EntityGuards.Required(selectedOptionKey, nameof(selectedOptionKey), "Selected option key is required.");
        IsCorrect = isCorrect;
    }

    public Guid ChallengeSubmissionAnswerId { get; private set; }

    public Guid ChallengeSubmissionId { get; private set; }

    public Guid ChallengeQuestionId { get; private set; }

    public string SelectedOptionKey { get; private set; } = string.Empty;

    public bool IsCorrect { get; private set; }

    public ChallengeSubmission Submission { get; private set; } = null!;

    public ChallengeQuestion Question { get; private set; } = null!;
}
