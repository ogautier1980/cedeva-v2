using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class ActivityDay : AuditableEntity
{
    public int DayId { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Label { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [DataType(DataType.Date)]
    public DateTime DayDate { get; set; }

    public int? Week { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsActive { get; set; }

    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public ICollection<BookingDay> BookingDays { get; set; } = new List<BookingDay>();
}
