using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class TeamMember
{
    public int TeamMemberId { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime BirthDate { get; set; }

    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(15, MinimumLength = 11)]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required]
    public TeamRole TeamRole { get; set; }

    [Required]
    public License License { get; set; }

    [Required]
    public Status Status { get; set; }

    public decimal? DailyCompensation { get; set; }

    [Required]
    [StringLength(100)]
    public string LicenseUrl { get; set; } = string.Empty;

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public string FullName => $"{LastName}, {FirstName}";
}
