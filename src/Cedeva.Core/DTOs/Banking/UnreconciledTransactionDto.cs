namespace Cedeva.Core.DTOs.Banking;

/// <summary>
/// DTO pour une transaction bancaire non rapprochée
/// </summary>
public class UnreconciledTransactionDto
{
    public int Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string? StructuredCommunication { get; set; }
    public string? FreeCommunication { get; set; }
    public string? CounterpartyName { get; set; }
    public string? CounterpartyAccount { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
}
