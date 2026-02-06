using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class ExcursionRegistrationsViewModel
{
    public Excursion Excursion { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
    public Dictionary<ActivityGroup, List<ExcursionChildInfo>> ChildrenByGroup { get; set; } = new();
}
