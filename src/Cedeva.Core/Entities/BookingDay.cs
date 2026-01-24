using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class BookingDay
{
    public int Id { get; set; }

    [Required]
    public int ActivityDayId { get; set; }
    public ActivityDay ActivityDay { get; set; } = null!;

    [Required]
    public bool IsReserved { get; set; }

    [Required]
    public bool IsPresent { get; set; }

    [Required]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
}
