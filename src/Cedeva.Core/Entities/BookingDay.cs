using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class BookingDay : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int ActivityDayId { get; set; }
    public ActivityDay ActivityDay { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    public bool IsReserved { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public bool IsPresent { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
}
