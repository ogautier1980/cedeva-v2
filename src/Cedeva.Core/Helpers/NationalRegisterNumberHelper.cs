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
    /// Checks if a national register number (formatted or unformatted) is a valid Belgian
    /// national register number: 11 digits, a plausible birth-date fragment, and a correct
    /// modulo-97 check number.
    /// </summary>
    /// <remarks>
    /// The check number equals <c>97 - (first 9 digits mod 97)</c>. For people born in or after
    /// 2000 a leading <c>2</c> is prepended to those 9 digits before the modulo. Since the century
    /// cannot always be inferred from the 2-digit year, a number is accepted if it matches EITHER
    /// rule — the standard validation approach.
    /// </remarks>
    public static bool IsValid(string? nationalRegisterNumber)
    {
        if (string.IsNullOrWhiteSpace(nationalRegisterNumber))
            return false;

        var digits = StripFormatting(nationalRegisterNumber);
        if (digits.Length != 11)
            return false;

        // Birth-date fragment plausibility (month 1-12, day 1-31). Belgian numbers can carry
        // 00 for an unknown month/day, so 0 is tolerated; clearly impossible values are rejected.
        var month = int.Parse(digits.Substring(2, 2));
        var day = int.Parse(digits.Substring(4, 2));
        if (month > 12 || day > 31)
            return false;

        var birthAndSequence = long.Parse(digits.Substring(0, 9));
        var checkNumber = int.Parse(digits.Substring(9, 2));

        var expectedBefore2000 = 97 - (int)(birthAndSequence % 97);
        // Prefixing "2" to the 9-digit number is the same as adding 2_000_000_000.
        var expectedFrom2000 = 97 - (int)((2_000_000_000L + birthAndSequence) % 97);

        return checkNumber == expectedBefore2000 || checkNumber == expectedFrom2000;
    }
}
