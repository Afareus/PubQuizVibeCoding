namespace QuizApp.Server.Domain.Entities;

internal static class EntityGuards
{
    public static string Required(string value, string paramName, string message)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(message, paramName)
            : value.Trim();
    }

    public static DateTime Utc(DateTime value, string paramName)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("Timestamp must be in UTC.", paramName);
    }

    public static int Range(int value, int minInclusive, int maxInclusive, string paramName, string message)
    {
        return value < minInclusive || value > maxInclusive
            ? throw new ArgumentOutOfRangeException(paramName, message)
            : value;
    }

    public static long NonNegative(long value, string paramName, string message)
    {
        return value < 0
            ? throw new ArgumentOutOfRangeException(paramName, message)
            : value;
    }
}