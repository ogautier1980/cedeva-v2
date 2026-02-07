namespace Cedeva.Core.Helpers;

/// <summary>
/// Helper for formatting and parsing Belgian National Register Numbers.
/// Format: YY.MM.DD-XXX.XX (e.g., 15.03.10-123.45)
/// Storage: YYMMDDXXXXX (11 digits only)
/// </summary>
public static class NationalRegisterNumberHelper
{
    /// <summary>
    /// Removes all formatting (dots and dashes) to get only digits.
    /// </summary>
    public static string StripFormatting(string? nationalRegisterNumber)
    {
        if (string.IsNullOrWhiteSpace(nationalRegisterNumber))
            return string.Empty;

        // Remove dots, dashes, and spaces
        return new string(nationalRegisterNumber.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// Formats a national register number with Belgian formatting: YY.MM.DD-XXX.XX
    /// </summary>
    public static string Format(string? nationalRegisterNumber)
    {
        if (string.IsNullOrWhiteSpace(nationalRegisterNumber))
            return string.Empty;

        // Strip any existing formatting
        var digitsOnly = StripFormatting(nationalRegisterNumber);

        // Need exactly 11 digits for formatting
        if (digitsOnly.Length != 11)
            return nationalRegisterNumber ?? string.Empty; // Return as-is if invalid length

        // Format: YY.MM.DD-XXX.XX
        return $"{digitsOnly.Substring(0, 2)}.{digitsOnly.Substring(2, 2)}.{digitsOnly.Substring(4, 2)}-{digitsOnly.Substring(6, 3)}.{digitsOnly.Substring(9, 2)}";
    }

    /// <summary>
    /// Checks if a national register number string (formatted or unformatted) is valid.
    /// </summary>
    public static bool IsValid(string? nationalRegisterNumber)
    {
        if (string.IsNullOrWhiteSpace(nationalRegisterNumber))
            return false;

        var digitsOnly = StripFormatting(nationalRegisterNumber);
        return digitsOnly.Length == 11 && digitsOnly.All(char.IsDigit);
    }
}
