using System.Globalization;

namespace Cedeva.Core.Helpers;

/// <summary>
/// Normalizes decimal amounts typed with either "." or "," as the decimal separator.
/// </summary>
/// <remarks>
/// The app's request culture is fr-BE, whose <see cref="NumberFormatInfo.NumberGroupSeparator"/> is
/// "." — so under a straight <c>decimal.Parse(value, CultureInfo.CurrentCulture)</c>, an amount typed
/// as "38.50" silently parses as 3850 (period read as a thousands separator) instead of failing loudly.
/// This normalizes the raw string first: whichever separator appears last is the decimal separator,
/// any earlier ones are thousands separators and get stripped.
/// </remarks>
public static class DecimalInputHelper
{
    public static bool TryParse(string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        var lastDot = trimmed.LastIndexOf('.');
        var lastComma = trimmed.LastIndexOf(',');
        var decimalSeparatorIndex = Math.Max(lastDot, lastComma);

        string normalized;
        if (decimalSeparatorIndex < 0)
        {
            normalized = trimmed;
        }
        else
        {
            var integerPart = trimmed[..decimalSeparatorIndex].Replace(".", "").Replace(",", "");
            var fractionalPart = trimmed[(decimalSeparatorIndex + 1)..];
            normalized = $"{integerPart}.{fractionalPart}";
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }
}
