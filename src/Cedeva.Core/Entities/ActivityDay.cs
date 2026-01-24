using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class ActivityDay
{
    public int DayId { get; set; }

    [Required]
    [StringLength(100)]
    public string Label { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime DayDate { get; set; }

    public int? Week { get; set; }

    [Required]
    public bool IsActive { get; set; }

    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public ICollection<BookingDay> BookingDays { get; set; } = new List<BookingDay>();
}
