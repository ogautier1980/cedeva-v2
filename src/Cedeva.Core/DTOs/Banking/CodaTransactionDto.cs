namespace Cedeva.Core.DTOs.Banking;

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
