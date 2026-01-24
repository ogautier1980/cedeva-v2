namespace Cedeva.Website.Features.Presence.ViewModels;

public class UpdatePresenceViewModel
{
    public int ActivityId { get; set; }
    public int ActivityDayId { get; set; }
    public Dictionary<int, bool> PresenceStatus { get; set; } = new();
}
