namespace Cedeva.Core.DTOs.Banking;

/// <summary>
/// DTO pour une suggestion de rapprochement semi-automatique
/// </summary>
public class ReconciliationSuggestionDto
{
    public int TransactionId { get; set; }
    public int BookingId { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal TransactionAmount { get; set; }
    public string? CounterpartyName { get; set; }
    public string? TransactionCommunication { get; set; } // Communication reçue (structured or free)
    public string? ExpectedCommunication { get; set; } // Communication structurée prévue
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public string ChildName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public DateTime BookingDate { get; set; }
    public decimal BookingRemainingAmount { get; set; }
    public int ConfidenceScore { get; set; } // 0-100
    public List<string> MatchReasons { get; set; } = new();
}
