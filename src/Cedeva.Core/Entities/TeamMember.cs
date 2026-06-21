using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class TeamMember : AuditableEntity
{
    public int TeamMemberId { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Date)]
    public DateTime BirthDate { get; set; }

    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(15, MinimumLength = 11, ErrorMessage = "Validation.StringLength")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    public TeamRole TeamRole { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public License License { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public Status Status { get; set; }

    public decimal? DailyCompensation { get; set; }

    // Required column (NOT NULL); defaults to empty so a member can be created without a license
    // file. Set to the uploaded file path on upload, reset to "" on removal.
    [StringLength(255)]
    public string LicenseUrl { get; set; } = string.Empty;

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public string FullName => $"{LastName}, {FirstName}";
}
