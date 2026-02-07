namespace Cedeva.Core.DTOs.Excursions;

/// <summary>
/// Information about a child for excursion attendance tracking.
/// </summary>
public class ExcursionAttendanceInfo
{
    public int RegistrationId { get; set; }
    public int BookingId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public bool IsPresent { get; set; }
}
