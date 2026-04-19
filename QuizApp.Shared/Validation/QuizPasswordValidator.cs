namespace QuizApp.Shared.Validation;

public static class QuizPasswordValidator
{
    public const int MinLength = 4;
    public const int MaxLength = 100;

    public static string? GetErrorMessage(string? password)
    {
        var trimmed = password?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return "Heslo kvízu je povinné.";
        }

        if (trimmed.Length < MinLength)
        {
            return $"Heslo musí mít alespoň {MinLength} znaky.";
        }

        if (trimmed.Length > MaxLength)
        {
            return $"Heslo může mít nejvýše {MaxLength} znaků.";
        }

        foreach (var ch in trimmed)
        {
            if (ch < 0x20)
            {
                return "Heslo obsahuje nepodporovaný znak. Povoleny jsou písmena, číslice, mezery a běžná interpunkce.";
            }
        }

        return null;
    }

    public static bool IsValid(string? password) => GetErrorMessage(password) is null;
}
