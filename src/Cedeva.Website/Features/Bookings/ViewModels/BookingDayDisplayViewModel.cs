namespace Cedeva.Website.Features.Bookings.ViewModels;

public class BookingDayDisplayViewModel
{
    public int ActivityDayId { get; set; }
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsReserved { get; set; }
    public bool IsPresent { get; set; }
}

public class WeeklyBookingDaysViewModel
{
    public int WeekNumber { get; set; }
    public string WeekLabel { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<BookingDayDisplayViewModel> Days { get; set; } = new();
}
