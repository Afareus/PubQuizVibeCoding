using QuizApp.Shared.Enums;

namespace QuizApp.Server.Domain.Entities;

public sealed class QuestionOption
{
    private QuestionOption()
    {
    }

    private QuestionOption(Guid questionOptionId, Guid questionId, OptionKey optionKey, string text)
    {
        if (questionOptionId == Guid.Empty)
        {
            throw new ArgumentException("Question option id must not be empty.", nameof(questionOptionId));
        }

        if (questionId == Guid.Empty)
        {
            throw new ArgumentException("Question id must not be empty.", nameof(questionId));
        }

        QuestionOptionId = questionOptionId;
        QuestionId = questionId;
        OptionKey = optionKey;
        Text = EntityGuards.Required(text, nameof(text), "Question option text is required.");
    }

    public Guid QuestionOptionId { get; private set; }

    public Guid QuestionId { get; private set; }

    public OptionKey OptionKey { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public Question? Question { get; private set; }

    public static QuestionOption Create(Guid questionOptionId, Guid questionId, OptionKey optionKey, string text)
    {
        return new QuestionOption(questionOptionId, questionId, optionKey, text);
    }
}