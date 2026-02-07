using Cedeva.Core.DTOs.Banking;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service pour parser et importer les fichiers CODA (relevés bancaires belges).
/// Format fixe de 128 caractères par ligne.
/// </summary>
public interface ICodaParserService
{
    /// <summary>
    /// Parse un fichier CODA et extrait toutes les informations.
    /// </summary>
    /// <param name="fileStream">Stream du fichier CODA</param>
    /// <param name="fileName">Nom du fichier</param>
    /// <returns>Données parsées du fichier CODA</returns>
    Task<CodaFileDto> ParseCodaFileAsync(Stream fileStream, string fileName);

    /// <summary>
    /// Importe un fichier CODA parsé dans la base de données.
    /// </summary>
    /// <param name="codaData">Données parsées</param>
    /// <param name="organisationId">ID de l'organisation</param>
    /// <param name="userId">ID de l'utilisateur qui importe</param>
    /// <returns>Fichier CODA enregistré</returns>
    Task<int> ImportCodaFileAsync(CodaFileDto codaData, int organisationId, int userId);
}
