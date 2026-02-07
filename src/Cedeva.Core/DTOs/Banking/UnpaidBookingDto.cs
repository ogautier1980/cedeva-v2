namespace Cedeva.Core.DTOs.Banking;

/// <summary>
/// DTO pour une réservation non ou partiellement payée
/// </summary>
public class UnpaidBookingDto
{
    public int Id { get; set; }
    public string? StructuredCommunication { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount => TotalAmount - PaidAmount;
    public string ChildName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public DateTime ActivityStartDate { get; set; }
}
