using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class OneReportViewModel
{
    public Activity Activity { get; set; } = null!;
    public List<OneChildListingRow> Under6Listing { get; set; } = new();
    public List<OneChildListingRow> Over6Listing { get; set; } = new();
    public List<OneWeekPresence> Under6Presences { get; set; } = new();
    public List<OneWeekPresence> Over6Presences { get; set; } = new();
}

public class OneChildListingRow
{
    public int Number { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime? FirstDay { get; set; }
    public DateTime? LastDay { get; set; }
    public int DaysCount { get; set; }
    public decimal AmountPaid { get; set; }
    public bool IsDisadvantagedEnvironment { get; set; }
    public bool IsMildDisability { get; set; }
    public bool IsSevereDisability { get; set; }
}

public class OneWeekPresence
{
    public int WeekNumber { get; set; }
    public List<OneDayPresence> Days { get; set; } = new();
}

public class OneDayPresence
{
    public DateTime DayDate { get; set; }
    public int PresentCount { get; set; }
}
