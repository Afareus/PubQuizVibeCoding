using System.ComponentModel.DataAnnotations;

namespace QuizApp.Server.Configuration;

public sealed class PostgreSqlOptions
{
    public const string SectionName = "PostgreSql";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;
}
