using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Child : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [DataType(DataType.Date)]
    public DateTime BirthDate { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(15, MinimumLength = 11, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsDisadvantagedEnvironment { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsMildDisability { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsSevereDisability { get; set; }

    public int ParentId { get; set; }
    public Parent Parent { get; set; } = null!;

    public int? ActivityGroupId { get; set; }
    public ActivityGroup? ActivityGroup { get; set; }

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public string FullName => $"{LastName}, {FirstName}";
}
