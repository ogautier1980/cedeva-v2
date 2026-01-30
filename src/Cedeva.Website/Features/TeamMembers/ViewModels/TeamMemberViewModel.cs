using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.TeamMembers.ViewModels;

public class TeamMemberViewModel
{
    public int TeamMemberId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [RegularExpression(@"^((\+32|0032)[\s\.\-\/]?|0)[\s\.\-\/]?4[789]([\s\.\-\/]?\d){7}$", ErrorMessage = "Validation.InvalidMobileNumber")]
    [Display(Name = "Field.MobilePhoneNumber")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(15, MinimumLength = 11)]
    [RegularExpression(@"^(\d{2})[.\- ]?(0[1-9]|1[0-2])[.\- ]?(0[1-9]|[12]\d|3[01])[.\- ]?(\d{3})[.\- ]?(\d{2})$")]
    [Display(Name = "Field.NationalRegisterNumber")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Field.BirthDate")]
    public DateTime BirthDate { get; set; }

    // Address
    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Field.Street")]
    public string Street { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Field.City")]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    [Display(Name = "Field.PostalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [Display(Name = "Field.Country")]
    public Country Country { get; set; } = Country.Belgium;

    // Team specific
    [Required]
    [Display(Name = "Field.TeamRole")]
    public TeamRole TeamRole { get; set; }

    [Required]
    [Display(Name = "Field.License")]
    public License License { get; set; }

    [Required]
    [Display(Name = "Field.Status")]
    public Status Status { get; set; }

    [Display(Name = "Field.DailyCompensation")]
    [Range(0, 10000)]
    [DataType(DataType.Currency)]
    public decimal? DailyCompensation { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Field.LicenseUrl")]
    public string LicenseUrl { get; set; } = string.Empty;

    public int? AddressId { get; set; }
    public int OrganisationId { get; set; }

    [Display(Name = "Field.FullName")]
    public string FullName => $"{LastName}, {FirstName}";

    // Summary counts
    [Display(Name = "Field.ActivitiesCount")]
    public int ActivitiesCount { get; set; }

    [Display(Name = "Field.ExpensesCount")]
    public int ExpensesCount { get; set; }
}
