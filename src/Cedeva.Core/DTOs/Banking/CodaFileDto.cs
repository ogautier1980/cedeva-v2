namespace Cedeva.Core.DTOs.Banking;

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
