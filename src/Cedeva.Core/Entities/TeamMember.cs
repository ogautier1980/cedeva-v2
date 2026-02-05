using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class TeamMember
{
    public int TeamMemberId { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [DataType(DataType.Date)]
    public DateTime BirthDate { get; set; }

    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(15, MinimumLength = 11, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    public TeamRole TeamRole { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public License License { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public Status Status { get; set; }

    public decimal? DailyCompensation { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string LicenseUrl { get; set; } = string.Empty;

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public string FullName => $"{LastName}, {FirstName}";
}
