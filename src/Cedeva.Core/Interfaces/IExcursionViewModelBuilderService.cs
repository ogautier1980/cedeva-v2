using Cedeva.Core.Entities;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service for building complex ViewModels for excursion views.
/// Encapsulates data grouping, sorting, and transformation logic.
/// </summary>
public interface IExcursionViewModelBuilderService
{
    /// <summary>
    /// Builds a dictionary of children grouped by ActivityGroup for the Registrations view.
    /// Includes registration status and payment information for each child.
    /// </summary>
    /// <param name="excursionId">The excursion ID</param>
    /// <param name="paymentStatusLocalizer">Function to localize PaymentStatus enum values</param>
    /// <returns>Dictionary with ActivityGroup keys and lists of child registration info</returns>
    Task<Dictionary<ActivityGroup, List<ExcursionChildInfo>>> BuildRegistrationsByGroupAsync(
        int excursionId,
        Func<PaymentStatus, string> paymentStatusLocalizer);

    /// <summary>
    /// Builds a dictionary of registered children grouped by ActivityGroup for the Attendance view.
    /// Includes attendance status for each child.
    /// </summary>
    /// <param name="excursionId">The excursion ID</param>
    /// <returns>Dictionary with ActivityGroup keys and lists of child attendance info</returns>
    Task<Dictionary<ActivityGroup, List<ExcursionAttendanceInfo>>> BuildAttendanceByGroupAsync(int excursionId);
}

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
