using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class BookingDay
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public int ActivityDayId { get; set; }
    public ActivityDay ActivityDay { get; set; } = null!;

    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsReserved { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsPresent { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
}
