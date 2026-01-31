namespace Cedeva.Website.Features.Bookings.ViewModels;

public class BookingDaySelectionViewModel
{
    public int ActivityDayId { get; set; }
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsSelected { get; set; }
    public bool IsWeekend { get; set; }
}
