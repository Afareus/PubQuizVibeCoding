namespace QuizApp.Shared.Contracts;

public static class CsvQuizContract
{
    public const string QuestionTextColumn = "question_text";
    public const string OptionAColumn = "option_a";
    public const string OptionBColumn = "option_b";
    public const string OptionCColumn = "option_c";
    public const string OptionDColumn = "option_d";
    public const string CorrectOptionColumn = "correct_option";
    public const string TimeLimitSecColumn = "time_limit_sec";

    public static readonly IReadOnlyList<string> Header =
    [
        QuestionTextColumn,
        OptionAColumn,
        OptionBColumn,
        OptionCColumn,
        OptionDColumn,
        CorrectOptionColumn,
        TimeLimitSecColumn
    ];
}
