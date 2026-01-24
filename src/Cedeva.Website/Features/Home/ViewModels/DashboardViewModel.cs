namespace Cedeva.Website.Features.Home.ViewModels;

public class DashboardViewModel
{
    public int TotalActivities { get; set; }
    public int ActiveActivities { get; set; }
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int TotalChildren { get; set; }
    public int TotalParents { get; set; }
    public int TotalTeamMembers { get; set; }

    public List<ActivitySummary> RecentActivities { get; set; } = new();
    public List<BookingSummary> RecentBookings { get; set; } = new();
}

public class ActivitySummary
{
    public int ActivityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int BookingsCount { get; set; }
}

public class BookingSummary
{
    public int BookingId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public bool IsConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
}
