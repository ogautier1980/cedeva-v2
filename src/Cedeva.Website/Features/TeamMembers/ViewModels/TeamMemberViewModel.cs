using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Cedeva.Website.Validation;
using Cedeva.Website.ViewModels;

namespace Cedeva.Website.Features.TeamMembers.ViewModels;

public class TeamMemberViewModel : AuditableViewModel
{
    public int TeamMemberId { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [EmailAddress]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [RegularExpression(@"^((\+32|0032)[\s\.\-\/]?|0)[\s\.\-\/]?4[789]([\s\.\-\/]?\d){7}$", ErrorMessage = "Validation.InvalidMobileNumber")]
    [Display(Name = "Field.MobilePhoneNumber")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(15, MinimumLength = 11, ErrorMessage = "Validation.StringLength")]
    [RegularExpression(@"^(\d{2})[.\- ]?(0[1-9]|1[0-2])[.\- ]?(0[1-9]|[12]\d|3[01])[.\- ]?(\d{3})[.\- ]?(\d{2})$")]
    [Display(Name = "Field.NationalRegisterNumber")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Date)]
    [Display(Name = "Field.BirthDate")]
    public DateTime BirthDate { get; set; }

    // Address
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Street")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.City")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(10, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.PostalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [Display(Name = "Field.Country")]
    public Country Country { get; set; } = Country.Belgium;

    // Team specific
    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.TeamRole")]
    public TeamRole TeamRole { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.License")]
    public License License { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.Status")]
    public Status Status { get; set; }

    [Display(Name = "Field.DailyCompensation")]
    [Range(0, 10000, ErrorMessage = "Validation.Range")]
    [DataType(DataType.Currency)]
    public decimal? DailyCompensation { get; set; }

    [Display(Name = "Field.LicenseFile")]
    [AllowedExtensions(".jpg", ".jpeg", ".png", ".gif", ".pdf")]
    [MaxFileSize(10 * 1024 * 1024)]
    public IFormFile? LicenseFile { get; set; }

    [Display(Name = "Field.RemoveLicense")]
    public bool RemoveLicense { get; set; }

    [StringLength(255)]
    [Display(Name = "Field.LicenseUrl")]
    public string? LicenseUrl { get; set; }

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
