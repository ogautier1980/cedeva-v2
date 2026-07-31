namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service pour générer des QR codes (ex. lien de paiement) sous forme d'image intégrable
/// directement dans un e-mail.
/// </summary>
public interface IQrCodeService
{
    /// <summary>
    /// Encode <paramref name="content"/> (typiquement une URL) en QR code PNG et renvoie une
    /// data URI (<c>data:image/png;base64,...</c>) prête à être utilisée comme <c>src</c> d'un
    /// <c>&lt;img&gt;</c> — aucun fichier ni route publique nécessaire.
    /// </summary>
    string GenerateDataUri(string content);
}
