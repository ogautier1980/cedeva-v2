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

/// <summary>
/// DTO représentant un fichier CODA parsé
/// </summary>
public class CodaFileDto
{
    public string FileName { get; set; } = string.Empty;
    public DateTime StatementDate { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal OldBalance { get; set; }
    public decimal NewBalance { get; set; }
    public List<CodaTransactionDto> Transactions { get; set; } = new();
}

/// <summary>
/// DTO représentant une transaction dans un fichier CODA
/// </summary>
public class CodaTransactionDto
{
    public DateTime TransactionDate { get; set; }
    public DateTime ValueDate { get; set; }
    public decimal Amount { get; set; }
    public string? StructuredCommunication { get; set; }
    public string? FreeCommunication { get; set; }
    public string? CounterpartyName { get; set; }
    public string? CounterpartyAccount { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
}
