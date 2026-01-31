namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service pour générer et valider les communications structurées belges.
/// Format: +++XXX/XXXX/XXXXX+++ (12 chiffres avec validation modulo 97)
/// </summary>
public interface IStructuredCommunicationService
{
    /// <summary>
    /// Génère une communication structurée unique pour une réservation.
    /// </summary>
    /// <param name="bookingId">ID de la réservation</param>
    /// <returns>Communication structurée au format +++XXX/XXXX/XXXXX+++</returns>
    string GenerateStructuredCommunication(int bookingId);

    /// <summary>
    /// Valide une communication structurée (checksum modulo 97).
    /// </summary>
    /// <param name="communication">Communication à valider</param>
    /// <returns>True si valide, false sinon</returns>
    bool ValidateStructuredCommunication(string communication);

    /// <summary>
    /// Extrait l'ID de réservation d'une communication structurée.
    /// </summary>
    /// <param name="communication">Communication structurée</param>
    /// <returns>ID de réservation si trouvé, null sinon</returns>
    int? ExtractBookingIdFromCommunication(string communication);
}
