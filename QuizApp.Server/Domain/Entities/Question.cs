using QuizApp.Shared.Enums;

namespace QuizApp.Server.Domain.Entities;

public sealed class Question
{
    private readonly List<QuestionOption> _options = [];

    private Question()
    {
    }

    private Question(Guid questionId, Guid quizId, int orderIndex, string text, int timeLimitSec, OptionKey correctOption)
    {
        if (questionId == Guid.Empty)
        {
            throw new ArgumentException("Question id must not be empty.", nameof(questionId));
        }

        if (quizId == Guid.Empty)
        {
            throw new ArgumentException("Quiz id must not be empty.", nameof(quizId));
        }

        QuestionId = questionId;
        QuizId = quizId;
        OrderIndex = EntityGuards.Range(orderIndex, 0, int.MaxValue, nameof(orderIndex), "Question order index must be non-negative.");
        Text = EntityGuards.Required(text, nameof(text), "Question text is required.");
        TimeLimitSec = EntityGuards.Range(timeLimitSec, 10, 300, nameof(timeLimitSec), "Question time limit must be between 10 and 300 seconds.");
        CorrectOption = correctOption;
    }

    public Guid QuestionId { get; private set; }

    public Guid QuizId { get; private set; }

    public int OrderIndex { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public int TimeLimitSec { get; private set; }

    public OptionKey CorrectOption { get; private set; }

    public Quiz? Quiz { get; private set; }

    public IReadOnlyCollection<QuestionOption> Options => _options;

    public static Question Create(Guid questionId, Guid quizId, int orderIndex, string text, int timeLimitSec, OptionKey correctOption)
    {
        return new Question(questionId, quizId, orderIndex, text, timeLimitSec, correctOption);
    }
}