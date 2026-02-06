using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class ActivityGroup : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Label { get; set; } = string.Empty;

    public int? Capacity { get; set; }

    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public ICollection<Child> Children { get; set; } = new List<Child>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
