using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Child
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime BirthDate { get; set; }

    [Required]
    [StringLength(15, MinimumLength = 11)]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required]
    public bool IsDisadvantagedEnvironment { get; set; }

    [Required]
    public bool IsMildDisability { get; set; }

    [Required]
    public bool IsSevereDisability { get; set; }

    public int ParentId { get; set; }
    public Parent Parent { get; set; } = null!;

    public int? ActivityGroupId { get; set; }
    public ActivityGroup? ActivityGroup { get; set; }

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public string FullName => $"{LastName}, {FirstName}";
}
