using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class ExcursionAttendanceViewModel
{
    public Excursion Excursion { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
    public Dictionary<ActivityGroup, List<ExcursionAttendanceInfo>> ChildrenByGroup { get; set; } = new();
}

public class ExcursionAttendanceInfo
{
    public int RegistrationId { get; set; }
    public int BookingId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public bool IsPresent { get; set; }
}
