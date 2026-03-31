using QuizApp.Server.Application.QuizImport;
using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Tests;

public class QuizCsvParserTests
{
    private readonly QuizCsvParser _parser = new();

    private const string ValidHeader = "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n";
    private const string ExtendedHeader = "question_text;question_type;option_a;option_b;option_c;option_d;correct_option;correct_numeric_value;time_limit_sec\n";

    [Fact]
    public void Parse_ValidCsv_ReturnsQuestionsWithoutValidationIssues()
    {
        const string csv =
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

        var result = _parser.Parse(csv);

        Assert.True(result.IsValid);
        Assert.Empty(result.ValidationIssues);
        Assert.Single(result.Questions);
        Assert.Equal("Kolik je 2+2?", result.Questions[0].QuestionText);
        Assert.Equal(QuestionType.MultipleChoice, result.Questions[0].QuestionType);
        Assert.Equal(OptionKey.B, result.Questions[0].CorrectOption);
        Assert.Equal(30, result.Questions[0].TimeLimitSec);
    }

    [Fact]
    public void Parse_ExtendedNumericRow_ReturnsNumericQuestion()
    {
        var csv =
            ExtendedHeader +
            "Kolik váží sud piva?;numeric;;;;;;49.8;30\n";

        var result = _parser.Parse(csv);

        Assert.True(result.IsValid);
        var question = Assert.Single(result.Questions);
        Assert.Equal(QuestionType.NumericClosest, question.QuestionType);
        Assert.Equal(49.8m, question.CorrectNumericValue);
        Assert.Null(question.CorrectOption);
    }

    [Fact]
    public void Parse_InvalidHeader_ReturnsHeaderValidationIssue()
    {
        const string csv =
            "question;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

        var result = _parser.Parse(csv);

        Assert.False(result.IsValid);
        Assert.Empty(result.Questions);
        var issue = Assert.Single(result.ValidationIssues);
        Assert.Equal(1, issue.Row);
        Assert.Equal("header", issue.Column);
        Assert.Equal(ApiErrorCode.CsvValidationFailed, issue.Code);
    }

    [Fact]
    public void Parse_InvalidDataRow_ReturnsColumnSpecificValidationIssues()
    {
        const string csv =
            "question_text;option_a;option_b;option_c;option_d;correct_option;time_limit_sec\n" +
            "Kolik je 2+2?;3;4;5;6;Z;5\n";

        var result = _parser.Parse(csv);

        Assert.False(result.IsValid);
        Assert.Empty(result.Questions);
        Assert.Contains(result.ValidationIssues, issue => issue.Row == 2 && issue.Column == CsvQuizContract.CorrectOptionColumn);
        Assert.Contains(result.ValidationIssues, issue => issue.Row == 2 && issue.Column == CsvQuizContract.TimeLimitSecColumn);
    }

    [Fact]
    public void Parse_EmptyLines_AreIgnored()
    {
        const string csv =
            ValidHeader +
            "\n" +
            "Kolik je 2+2?;3;4;5;6;B;30\n" +
            ";;;;;;\n" +
            "Hlavní město ČR?;Brno;Plzeň;Praha;Ostrava;C;45\n";

        var result = _parser.Parse(csv);

        Assert.True(result.IsValid);
        Assert.Empty(result.ValidationIssues);
        Assert.Equal(2, result.Questions.Count);
        Assert.Equal(3, result.Questions[0].SourceRow);
        Assert.Equal(5, result.Questions[1].SourceRow);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsMissingHeaderIssue()
    {
        var result = _parser.Parse(string.Empty);

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.ValidationIssues);
        Assert.Equal(1, issue.Row);
        Assert.Equal("header", issue.Column);
    }

    [Fact]
    public void Parse_RowWithInvalidColumnCount_ReturnsRowIssue()
    {
        const string csv =
            ValidHeader +
            "Pouze šest sloupců;a;b;c;d;A\n";

        var result = _parser.Parse(csv);

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.ValidationIssues);
        Assert.Equal(2, issue.Row);
        Assert.Equal("row", issue.Column);
        Assert.Empty(result.Questions);
    }

    [Fact]
    public void Parse_MissingRequiredTextFields_ReturnsIssueForEachMissingColumn()
    {
        const string csv =
            ValidHeader +
            "Otázka;; ;\t;;B;60\n";

        var result = _parser.Parse(csv);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationIssues, issue => issue.Row == 2 && issue.Column == CsvQuizContract.OptionAColumn);
        Assert.Contains(result.ValidationIssues, issue => issue.Row == 2 && issue.Column == CsvQuizContract.OptionBColumn);
        Assert.Contains(result.ValidationIssues, issue => issue.Row == 2 && issue.Column == CsvQuizContract.OptionCColumn);
        Assert.Contains(result.ValidationIssues, issue => issue.Row == 2 && issue.Column == CsvQuizContract.OptionDColumn);
        Assert.Empty(result.Questions);
    }

    [Theory]
    [InlineData("a", OptionKey.A)]
    [InlineData("B", OptionKey.B)]
    [InlineData(" c ", OptionKey.C)]
    [InlineData("D", OptionKey.D)]
    public void Parse_CorrectOption_IsTrimmedAndCaseInsensitive(string input, OptionKey expected)
    {
        var csv =
            ValidHeader +
            $"Otázka;a;b;c;d;{input};30\n";

        var result = _parser.Parse(csv);

        Assert.True(result.IsValid);
        Assert.Single(result.Questions);
        Assert.Equal(expected, result.Questions[0].CorrectOption);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(300)]
    public void Parse_TimeLimit_BoundaryValues_AreAccepted(int timeLimit)
    {
        var csv =
            ValidHeader +
            $"Otázka;a;b;c;d;A;{timeLimit}\n";

        var result = _parser.Parse(csv);

        Assert.True(result.IsValid);
        Assert.Single(result.Questions);
        Assert.Equal(timeLimit, result.Questions[0].TimeLimitSec);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("301")]
    [InlineData("abc")]
    public void Parse_TimeLimit_InvalidValues_ReturnValidationIssue(string timeLimit)
    {
        var csv =
            ValidHeader +
            $"Otázka;a;b;c;d;A;{timeLimit}\n";

        var result = _parser.Parse(csv);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationIssues, issue => issue.Row == 2 && issue.Column == CsvQuizContract.TimeLimitSecColumn);
        Assert.Empty(result.Questions);
    }

    [Fact]
    public void Parse_QuotedFieldsWithSemicolonAndEscapedQuotes_AreParsedCorrectly()
    {
        const string csv =
            ValidHeader +
            "\"Otázka; se středníkem\";\"A\"\"1\"\"\";B;C;D;A;20\n";

        var result = _parser.Parse(csv);

        Assert.True(result.IsValid);
        var question = Assert.Single(result.Questions);
        Assert.Equal("Otázka; se středníkem", question.QuestionText);
        Assert.Equal("A\"1\"", question.OptionA);
    }

    [Fact]
    public void Parse_WithExcelSeparatorDirective_IsAccepted()
    {
        const string csv =
            "sep=;\n" +
            ValidHeader +
            "Kolik je 2+2?;3;4;5;6;B;30\n";

        var result = _parser.Parse(csv);

        Assert.True(result.IsValid);
        var question = Assert.Single(result.Questions);
        Assert.Equal("Kolik je 2+2?", question.QuestionText);
    }

    [Fact]
    public void Parse_MixedValidAndInvalidRows_ReturnsValidQuestionsAndValidationIssues()
    {
        const string csv =
            ValidHeader +
            "Validní otázka;a;b;c;d;A;20\n" +
            "Nevalidní otázka;a;b;c;d;Z;20\n" +
            "Druhá validní otázka;a;b;c;d;D;25\n";

        var result = _parser.Parse(csv);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Questions.Count);
        Assert.Single(result.ValidationIssues);
        Assert.Equal(3, result.ValidationIssues[0].Row);
        Assert.Equal(CsvQuizContract.CorrectOptionColumn, result.ValidationIssues[0].Column);
    }
}
