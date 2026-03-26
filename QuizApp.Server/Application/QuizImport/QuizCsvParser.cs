using QuizApp.Shared.Contracts;
using QuizApp.Shared.Enums;

namespace QuizApp.Server.Application.QuizImport;

public interface IQuizCsvParser
{
    CsvQuizImportParseResult Parse(string csvContent);
}

public sealed class QuizCsvParser : IQuizCsvParser
{
    private const int MinimumTimeLimitSec = 10;
    private const int MaximumTimeLimitSec = 300;

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
        if (!HasValidHeader(headerRecord.Cells))
        {
            issues.Add(CreateIssue(headerRecord.Row, "header", $"Neplatná CSV hlavička. Očekávaný formát: {string.Join(",", CsvQuizContract.Header)}."));
            return new CsvQuizImportParseResult(parsedQuestions, issues);
        }

        for (var index = 1; index < records.Count; index++)
        {
            var record = records[index];

            if (record.Cells.Count != CsvQuizContract.Header.Count)
            {
                issues.Add(CreateIssue(record.Row, "row", $"Řádek musí obsahovat přesně {CsvQuizContract.Header.Count} sloupců."));
                continue;
            }

            var questionText = record.Cells[0].Trim();
            var optionA = record.Cells[1].Trim();
            var optionB = record.Cells[2].Trim();
            var optionC = record.Cells[3].Trim();
            var optionD = record.Cells[4].Trim();
            var correctOptionRaw = record.Cells[5].Trim();
            var timeLimitRaw = record.Cells[6].Trim();

            var hasRowIssues = false;
            hasRowIssues |= ValidateRequired(questionText, CsvQuizContract.QuestionTextColumn, record.Row, issues);
            hasRowIssues |= ValidateRequired(optionA, CsvQuizContract.OptionAColumn, record.Row, issues);
            hasRowIssues |= ValidateRequired(optionB, CsvQuizContract.OptionBColumn, record.Row, issues);
            hasRowIssues |= ValidateRequired(optionC, CsvQuizContract.OptionCColumn, record.Row, issues);
            hasRowIssues |= ValidateRequired(optionD, CsvQuizContract.OptionDColumn, record.Row, issues);

            if (!TryParseOption(correctOptionRaw, out var correctOption))
            {
                hasRowIssues = true;
                issues.Add(CreateIssue(record.Row, CsvQuizContract.CorrectOptionColumn, "Hodnota correct_option musí být jedna z hodnot A, B, C, D."));
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
                correctOption,
                timeLimitSec,
                record.Row));
        }

        return new CsvQuizImportParseResult(parsedQuestions, issues);
    }

    private static bool HasValidHeader(IReadOnlyList<string> headerCells)
    {
        if (headerCells.Count != CsvQuizContract.Header.Count)
        {
            return false;
        }

        for (var index = 0; index < headerCells.Count; index++)
        {
            if (!string.Equals(headerCells[index].Trim(), CsvQuizContract.Header[index], StringComparison.Ordinal))
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

            if (!inQuotes && character == ',')
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
    OptionKey CorrectOption,
    int TimeLimitSec,
    int SourceRow);
