using Cedeva.Core.Interfaces;
using System.Text.RegularExpressions;

namespace Cedeva.Infrastructure.Services;

/// <summary>
/// Implémentation du service de communications structurées belges.
/// Format: +++XXX/XXXX/XXXXX+++ où les 2 derniers chiffres sont le checksum modulo 97.
/// </summary>
public partial class StructuredCommunicationService : IStructuredCommunicationService
{
    [GeneratedRegex(@"^\+\+\+(\d{3})/(\d{4})/(\d{5})\+\+\+$")]
    private static partial Regex StructuredCommunicationRegex();

    public string GenerateStructuredCommunication(int bookingId)
    {
        // Padding sur 10 chiffres (les 2 derniers seront le checksum)
        string bookingIdPadded = bookingId.ToString().PadLeft(10, '0');

        // Calcul du modulo 97
        long number = long.Parse(bookingIdPadded);
        int checksum = (int)(number % 97);

        // Si modulo = 0, utiliser 97
        if (checksum == 0)
        {
            checksum = 97;
        }

        // Format final: les 10 chiffres + checksum sur 2 chiffres
        string fullNumber = bookingIdPadded + checksum.ToString().PadLeft(2, '0');

        // Formatter selon le pattern XXX/XXXX/XXXXX
        string formatted = $"{fullNumber.Substring(0, 3)}/{fullNumber.Substring(3, 4)}/{fullNumber.Substring(7, 5)}";

        return $"+++{formatted}+++";
    }

    public bool ValidateStructuredCommunication(string communication)
    {
        if (string.IsNullOrWhiteSpace(communication))
        {
            return false;
        }

        var match = StructuredCommunicationRegex().Match(communication);
        if (!match.Success)
        {
            return false;
        }

        // Extraire les chiffres (sans les '/' et '+++')
        string digits = match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value;

        // Les 10 premiers chiffres
        string baseNumber = digits.Substring(0, 10);

        // Les 2 derniers chiffres (checksum)
        int providedChecksum = int.Parse(digits.Substring(10, 2));

        // Calcul du checksum attendu
        long number = long.Parse(baseNumber);
        int expectedChecksum = (int)(number % 97);
        if (expectedChecksum == 0)
        {
            expectedChecksum = 97;
        }

        return providedChecksum == expectedChecksum;
    }

    public int? ExtractBookingIdFromCommunication(string communication)
    {
        if (!ValidateStructuredCommunication(communication))
        {
            return null;
        }

        var match = StructuredCommunicationRegex().Match(communication);
        if (!match.Success)
        {
            return null;
        }

        // Extraire les 10 premiers chiffres (ID du booking)
        string digits = match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value;
        string bookingIdString = digits.Substring(0, 10);

        if (int.TryParse(bookingIdString, out int bookingId))
        {
            return bookingId;
        }

        return null;
    }
}
