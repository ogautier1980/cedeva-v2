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
}

public class PrintPresencesViewModel
{
    public Activity Activity { get; set; } = null!;
    public ActivityDay ActivityDay { get; set; } = null!;
    public List<PresenceChildInfo> PresenceItems { get; set; } = new();
}
