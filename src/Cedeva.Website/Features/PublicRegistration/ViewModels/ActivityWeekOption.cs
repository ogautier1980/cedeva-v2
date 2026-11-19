namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

/// <summary>
/// One selectable week of a (possibly multi-week) activity on the public registration form — a
/// child registers for one or more weeks, not necessarily the whole activity.
/// </summary>
public class ActivityWeekOption
{
    public int WeekNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Ids of the activity's active days that fall in this week.</summary>
    public List<int> DayIds { get; set; } = new();

    /// <summary>The same days with their dates, for the day-by-day picker.</summary>
    public List<(int DayId, DateTime Date)> Days { get; set; } = new();

    /// <summary>Days of this week that reached the activity's MaxChildrenPerDay (greyed out in the forms).</summary>
    public HashSet<int> FullDayIds { get; set; } = new();

    /// <summary>A week is booked as a whole: one full day makes it unavailable.</summary>
    public bool HasFullDay => FullDayIds.Count > 0;
}
