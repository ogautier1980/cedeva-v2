namespace Cedeva.Website.Features.Home.ViewModels;

public class DashboardViewModel
{
    public List<ActivitySummary> RecentActivities { get; set; } = new();
}

public class ActivitySummary
{
    public int ActivityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int BookingsCount { get; set; }
}
