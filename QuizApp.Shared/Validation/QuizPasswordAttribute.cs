using System.ComponentModel.DataAnnotations;

namespace QuizApp.Shared.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class QuizPasswordAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var error = QuizPasswordValidator.GetErrorMessage(value as string);
        if (error is null)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(error, [validationContext.MemberName ?? string.Empty]);
    }
}
