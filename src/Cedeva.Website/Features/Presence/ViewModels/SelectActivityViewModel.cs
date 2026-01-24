using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.Presence.ViewModels;

public class SelectActivityViewModel
{
    public List<Activity> Activities { get; set; } = new();
    public int? SelectedActivityId { get; set; }
}
