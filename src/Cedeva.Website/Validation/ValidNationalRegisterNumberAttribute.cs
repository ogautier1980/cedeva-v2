using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Helpers;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Validation;

/// <summary>
/// Validates that a property holds a valid Belgian national register number (11 digits,
/// plausible birth date and correct modulo-97 check number), via
/// <see cref="NationalRegisterNumberHelper.IsValid"/>.
///
/// Emptiness is intentionally NOT enforced here so the attribute composes with a separate
/// <c>[Required]</c> where the field is mandatory: a blank value passes (let [Required] decide),
/// a non-blank value must be valid. The error message is localized via SharedResources
/// (key <c>Validation.NRNInvalid</c>) with an English fallback.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidNationalRegisterNumberAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var input = value as string;

        // Blank is handled by [Required]; only validate the format/checksum when a value is present.
        if (string.IsNullOrWhiteSpace(input))
            return ValidationResult.Success;

        return NationalRegisterNumberHelper.IsValid(input)
            ? ValidationResult.Success
            : new ValidationResult(GetErrorMessage(validationContext), GetMemberNames(validationContext));
    }

    private static string[]? GetMemberNames(ValidationContext context) =>
        string.IsNullOrEmpty(context.MemberName) ? null : [context.MemberName];

    private static string GetErrorMessage(ValidationContext validationContext)
    {
        var localizer = validationContext.GetService(typeof(IStringLocalizer<Localization.SharedResources>))
            as IStringLocalizer<Localization.SharedResources>;

        return localizer?["Validation.NRNInvalid"].Value
            ?? "Invalid Belgian national register number.";
    }
}
