using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.Presence.ViewModels;

public class PresenceListViewModel
{
    public Activity Activity { get; set; } = null!;
    public ActivityDay ActivityDay { get; set; } = null!;
    public List<PresenceItemViewModel> PresenceItems { get; set; } = new();
}

public class PresenceItemViewModel
{
    public int BookingDayId { get; set; }
    public int BookingId { get; set; }
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public DateTime ChildBirthDate { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public bool IsReserved { get; set; }
    public bool IsPresent { get; set; }
    public string? ActivityGroupName { get; set; }
}
