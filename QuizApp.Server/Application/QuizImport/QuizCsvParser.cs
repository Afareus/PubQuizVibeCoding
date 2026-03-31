using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;
using System.Globalization;

namespace QuizApp.Server.Application.QuizImport;

public interface IQuizCsvParser
{
    CsvQuizImportParseResult Parse(string csvContent);
}

public sealed class QuizCsvParser : IQuizCsvParser
{
    private const int MinimumTimeLimitSec = 10;
    private const int MaximumTimeLimitSec = 300;
    private const char CsvDelimiter = ';';
    private const string ExcelSeparatorDirective = "sep=;";

    public CsvQuizImportParseResult Parse(string csvContent)
    {
        var issues = new List<CsvValidationIssueDto>();
        var parsedQuestions = new List<ParsedCsvQuestion>();
        var records = ReadRecords(csvContent);

        if (records.Count == 0)
        {
            issues.Add(CreateIssue(1, "header", "CSV soubor neobsahuje povinnou hlavičku."));
            return new CsvQuizImportParseResult(parsedQuestions, issues);
        }

        var headerRecord = records[0];
        var headerKind = ResolveHeaderKind(headerRecord.Cells);
        if (headerKind == CsvHeaderKind.Invalid)
        {
            issues.Add(CreateIssue(headerRecord.Row, "header", $"Neplatná CSV hlavička. Očekávaný formát: {string.Join(CsvDelimiter, CsvQuizContract.Header)} nebo {string.Join(CsvDelimiter, CsvQuizContract.ExtendedHeader)}."));
            return new CsvQuizImportParseResult(parsedQuestions, issues);
        }

        for (var index = 1; index < records.Count; index++)
        {
            var record = records[index];

            var expectedColumnCount = headerKind == CsvHeaderKind.Extended
                ? CsvQuizContract.ExtendedHeader.Count
                : CsvQuizContract.Header.Count;

            if (record.Cells.Count != expectedColumnCount)
            {
                issues.Add(CreateIssue(record.Row, "row", $"Řádek musí obsahovat přesně {expectedColumnCount} sloupců."));
                continue;
            }

            var questionText = record.Cells[0].Trim();
            var questionTypeRaw = headerKind == CsvHeaderKind.Extended ? record.Cells[1].Trim() : "choice";
            var optionOffset = headerKind == CsvHeaderKind.Extended ? 2 : 1;

            var optionA = record.Cells[optionOffset].Trim();
            var optionB = record.Cells[optionOffset + 1].Trim();
            var optionC = record.Cells[optionOffset + 2].Trim();
            var optionD = record.Cells[optionOffset + 3].Trim();
            var correctOptionRaw = record.Cells[optionOffset + 4].Trim();
            var correctNumericRaw = headerKind == CsvHeaderKind.Extended ? record.Cells[optionOffset + 5].Trim() : string.Empty;
            var timeLimitRaw = record.Cells[optionOffset + (headerKind == CsvHeaderKind.Extended ? 6 : 5)].Trim();

            var hasRowIssues = false;
            hasRowIssues |= ValidateRequired(questionText, CsvQuizContract.QuestionTextColumn, record.Row, issues);

            if (!TryParseQuestionType(questionTypeRaw, out var questionType))
            {
                hasRowIssues = true;
                issues.Add(CreateIssue(record.Row, CsvQuizContract.QuestionTypeColumn, "Hodnota question_type musí být 'choice' nebo 'numeric'."));
            }

            OptionKey? correctOption = null;
            decimal? correctNumericValue = null;

            if (questionType == QuestionType.MultipleChoice)
            {
                hasRowIssues |= ValidateRequired(optionA, CsvQuizContract.OptionAColumn, record.Row, issues);
                hasRowIssues |= ValidateRequired(optionB, CsvQuizContract.OptionBColumn, record.Row, issues);
                hasRowIssues |= ValidateRequired(optionC, CsvQuizContract.OptionCColumn, record.Row, issues);
                hasRowIssues |= ValidateRequired(optionD, CsvQuizContract.OptionDColumn, record.Row, issues);

                if (!TryParseOption(correctOptionRaw, out var parsedCorrectOption))
                {
                    hasRowIssues = true;
                    issues.Add(CreateIssue(record.Row, CsvQuizContract.CorrectOptionColumn, "Hodnota correct_option musí být jedna z hodnot A, B, C, D."));
                }
                else
                {
                    correctOption = parsedCorrectOption;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(correctNumericRaw))
                {
                    hasRowIssues = true;
                    issues.Add(CreateIssue(record.Row, CsvQuizContract.CorrectNumericValueColumn, "Sloupec correct_numeric_value je povinný pro question_type=numeric."));
                }
                else if (!decimal.TryParse(correctNumericRaw.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedNumericValue))
                {
                    hasRowIssues = true;
                    issues.Add(CreateIssue(record.Row, CsvQuizContract.CorrectNumericValueColumn, "Hodnota correct_numeric_value musí být číslo."));
                }
                else
                {
                    correctNumericValue = parsedNumericValue;
                }
            }

            if (!int.TryParse(timeLimitRaw, out var timeLimitSec) || timeLimitSec < MinimumTimeLimitSec || timeLimitSec > MaximumTimeLimitSec)
            {
                hasRowIssues = true;
                issues.Add(CreateIssue(record.Row, CsvQuizContract.TimeLimitSecColumn, "Hodnota time_limit_sec musí být celé číslo v rozsahu 10 až 300."));
            }

            if (hasRowIssues)
            {
                continue;
            }

            parsedQuestions.Add(new ParsedCsvQuestion(
                questionText,
                optionA,
                optionB,
                optionC,
                optionD,
                questionType,
                correctOption,
                correctNumericValue,
                timeLimitSec,
                record.Row));
        }

        return new CsvQuizImportParseResult(parsedQuestions, issues);
    }

    private static CsvHeaderKind ResolveHeaderKind(IReadOnlyList<string> headerCells)
    {
        if (MatchesHeader(headerCells, CsvQuizContract.Header))
        {
            return CsvHeaderKind.Legacy;
        }

        if (MatchesHeader(headerCells, CsvQuizContract.ExtendedHeader))
        {
            return CsvHeaderKind.Extended;
        }

        return CsvHeaderKind.Invalid;
    }

    private static bool MatchesHeader(IReadOnlyList<string> headerCells, IReadOnlyList<string> expectedHeader)
    {
        if (headerCells.Count != expectedHeader.Count)
        {
            return false;
        }

        for (var index = 0; index < headerCells.Count; index++)
        {
            if (!string.Equals(headerCells[index].Trim(), expectedHeader[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateRequired(string value, string column, int row, ICollection<CsvValidationIssueDto> issues)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        issues.Add(CreateIssue(row, column, $"Sloupec {column} je povinný."));
        return true;
    }

    private static bool TryParseQuestionType(string value, out QuestionType questionType)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "choice":
            case "multiplechoice":
                questionType = QuestionType.MultipleChoice;
                return true;
            case "numeric":
            case "numericclosest":
                questionType = QuestionType.NumericClosest;
                return true;
            default:
                questionType = default;
                return false;
        }
    }

    private static bool TryParseOption(string value, out OptionKey optionKey)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "A":
                optionKey = OptionKey.A;
                return true;
            case "B":
                optionKey = OptionKey.B;
                return true;
            case "C":
                optionKey = OptionKey.C;
                return true;
            case "D":
                optionKey = OptionKey.D;
                return true;
            default:
                optionKey = default;
                return false;
        }
    }

    private static CsvValidationIssueDto CreateIssue(int row, string column, string message)
    {
        return new CsvValidationIssueDto(row, column, ApiErrorCode.CsvValidationFailed, message);
    }

    private static IReadOnlyList<CsvRecord> ReadRecords(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return [];
        }

        var records = new List<CsvRecord>();
        using var reader = new StringReader(csvContent);

        string? line;
        var lineNumber = 0;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (records.Count == 0 && string.Equals(line.Trim(), ExcelSeparatorDirective, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cells = ParseLine(line);
            if (cells.All(static value => string.IsNullOrWhiteSpace(value)))
            {
                continue;
            }

            records.Add(new CsvRecord(lineNumber, cells));
        }

        return records;
    }

    private static List<string> ParseLine(string line)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && character == CsvDelimiter)
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        cells.Add(current.ToString());
        return cells;
    }

    private sealed record CsvRecord(int Row, IReadOnlyList<string> Cells);

    private enum CsvHeaderKind
    {
        Invalid = 0,
        Legacy = 1,
        Extended = 2
    }
}

public sealed record CsvQuizImportParseResult(
    IReadOnlyList<ParsedCsvQuestion> Questions,
    IReadOnlyList<CsvValidationIssueDto> ValidationIssues)
{
    public bool IsValid => ValidationIssues.Count == 0;
}

public sealed record ParsedCsvQuestion(
    string QuestionText,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    QuestionType QuestionType,
    OptionKey? CorrectOption,
    decimal? CorrectNumericValue,
    int TimeLimitSec,
    int SourceRow);
