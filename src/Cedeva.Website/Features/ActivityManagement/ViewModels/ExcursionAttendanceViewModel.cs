using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class ExcursionAttendanceViewModel
{
    public Excursion Excursion { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
    public Dictionary<ActivityGroup, List<ExcursionAttendanceInfo>> ChildrenByGroup { get; set; } = new();
}
