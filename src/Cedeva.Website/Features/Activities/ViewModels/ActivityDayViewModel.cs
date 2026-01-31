namespace Cedeva.Website.Features.Activities.ViewModels;

public class ActivityDayViewModel
{
    public int DayId { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime DayDate { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsActive { get; set; }
    public bool IsWeekend { get; set; }
    public int? Week { get; set; }
}

public class WeeklyActivityDaysViewModel
{
    public int WeekNumber { get; set; }
    public string WeekLabel { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<ActivityDayViewModel> Days { get; set; } = new();
    public int ActiveDaysCount { get; set; }
    public int TotalDaysCount { get; set; }
}
