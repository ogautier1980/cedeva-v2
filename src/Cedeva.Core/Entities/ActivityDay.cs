using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class ActivityDay : AuditableEntity
{
    public int DayId { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string Label { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Date)]
    public DateTime DayDate { get; set; }

    public int? Week { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public bool IsActive { get; set; }

    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public ICollection<BookingDay> BookingDays { get; set; } = new List<BookingDay>();
}
