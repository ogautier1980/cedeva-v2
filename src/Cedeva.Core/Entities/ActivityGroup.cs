using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class ActivityGroup
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Label { get; set; } = string.Empty;

    public int? Capacity { get; set; }

    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public ICollection<Child> Children { get; set; } = new List<Child>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
