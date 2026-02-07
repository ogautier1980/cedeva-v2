namespace Cedeva.Core.DTOs.Excursions;

/// <summary>
/// Information about a child for excursion registration management.
/// </summary>
public class ExcursionChildInfo
{
    public int BookingId { get; set; }
    public int ChildId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public bool IsRegistered { get; set; }
    public int? RegistrationId { get; set; }
    public decimal ExcursionCost { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}
