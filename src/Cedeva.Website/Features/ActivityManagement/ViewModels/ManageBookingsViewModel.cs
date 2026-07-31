using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class ManageBookingsViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<BookingManagementItem> Bookings { get; set; } = new();

    // Summary count for the dashboard badge
    public int PendingConfirmationCount { get; set; }
}

public class BookingManagementItem
{
    public int BookingId { get; set; }
    public int ChildId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public int Age => DateTime.Today.Year - BirthDate.Year - (DateTime.Today.DayOfYear < BirthDate.DayOfYear ? 1 : 0);

    public bool IsConfirmed { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal Balance => TotalAmount - PaidAmount;
}
