using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class ManageBookingsViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<BookingManagementItem> Bookings { get; set; } = new();
    public List<SelectListItem> GroupOptions { get; set; } = new();

    // Summary counts for the dashboard badge
    public int PendingConfirmationCount { get; set; }
    public int WithoutGroupCount { get; set; }
    public int WithoutMedicalSheetCount { get; set; }
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
    public int? GroupId { get; set; }
    public string? GroupLabel { get; set; }
    public bool IsMedicalSheet { get; set; }

    // Flags to determine what needs attention
    public bool NeedsConfirmation => !IsConfirmed;
    public bool NeedsGroup => GroupId == null || GroupLabel == "Sans groupe";
    public bool NeedsMedicalSheet => !IsMedicalSheet;
}
