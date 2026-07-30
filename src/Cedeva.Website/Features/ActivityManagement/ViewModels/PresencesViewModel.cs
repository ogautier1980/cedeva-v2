using Cedeva.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class PresencesViewModel
{
    public Activity Activity { get; set; } = null!;
    public int? SelectedActivityDayId { get; set; }
    public ActivityDay? SelectedActivityDay { get; set; }
    public List<SelectListItem> ActivityDayOptions { get; set; } = new();
    public List<PresenceChildInfo> Children { get; set; } = new();
}

public class PresenceChildInfo
{
    public int BookingId { get; set; }
    public int ChildId { get; set; }
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public DateTime ChildBirthDate { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public bool IsReserved { get; set; }
    public bool IsPresent { get; set; }
    public int? BookingDayId { get; set; }
    public string? ActivityGroupName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public bool IsFullyPaid => PaidAmount >= TotalAmount;
    public decimal Balance => TotalAmount - PaidAmount;
}

public class PrintPresencesViewModel
{
    public Activity Activity { get; set; } = null!;
    public ActivityDay ActivityDay { get; set; } = null!;
    public List<PresenceChildInfo> PresenceItems { get; set; } = new();
}

public class GroupsRosterViewModel
{
    public Activity Activity { get; set; } = null!;
    public List<int> SelectedGroupIds { get; set; } = new();
    public int? SelectedActivityDayId { get; set; }
    public int? TodayActivityDayId { get; set; }
    public List<SelectListItem> GroupOptions { get; set; } = new();
    public List<SelectListItem> ActivityDayOptions { get; set; } = new();
    public List<PresenceChildInfo> Children { get; set; } = new();
    public bool ShowReserved { get; set; } = true;
    public bool ShowPresent { get; set; } = true;
    public bool ShowSignature { get; set; }
}

public class PrintGroupsViewModel
{
    public Activity Activity { get; set; } = null!;
    public List<ActivityGroup> Groups { get; set; } = new();
    public ActivityDay? ActivityDay { get; set; }
    public List<PresenceChildInfo> Children { get; set; } = new();
    public bool ShowReserved { get; set; } = true;
    public bool ShowPresent { get; set; } = true;
    public bool ShowSignature { get; set; }
}

public class PresenceSummaryViewModel
{
    public Activity Activity { get; set; } = null!;
    public List<DayPresenceSummary> Days { get; set; } = new();
}

public class DayPresenceSummary
{
    public DateTime DayDate { get; set; }
    public string Label { get; set; } = string.Empty;
    public int ReservedCount { get; set; }
    public int PresentCount { get; set; }
    public int ReservedDisadvantagedCount { get; set; }
    public int PresentDisadvantagedCount { get; set; }
    public int ReservedMildDisabilityCount { get; set; }
    public int PresentMildDisabilityCount { get; set; }
    public int ReservedSevereDisabilityCount { get; set; }
    public int PresentSevereDisabilityCount { get; set; }
}
