using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.Presence.ViewModels;

public class SelectDayViewModel
{
    public Activity Activity { get; set; } = null!;
    public List<ActivityDay> ActivityDays { get; set; } = new();
    public int? SelectedDayId { get; set; }
}
