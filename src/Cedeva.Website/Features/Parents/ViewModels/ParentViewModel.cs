using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Parents.ViewModels;

public class ParentViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [EmailAddress]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [RegularExpression(@"^((\+32|0032)[\s\.\-\/]?|0)[\s\.\-\/]?\d([\s\.\-\/]?\d){7}$", ErrorMessage = "Validation.InvalidLandlineNumber")]
    [Display(Name = "Field.LandlineNumber")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [RegularExpression(@"^((\+32|0032)[\s\.\-\/]?|0)[\s\.\-\/]?4[789]([\s\.\-\/]?\d){7}$", ErrorMessage = "Validation.InvalidMobileNumber")]
    [Display(Name = "Field.MobilePhoneNumber")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(15, MinimumLength = 11, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [RegularExpression(@"^(\d{2})[.\- ]?(0[1-9]|1[0-2])[.\- ]?(0[1-9]|[12]\d|3[01])[.\- ]?(\d{3})[.\- ]?(\d{2})$")]
    [Display(Name = "Field.NationalRegisterNumber")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    // Address
    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Street")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.City")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(10, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.PostalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [Display(Name = "Field.Country")]
    public Country Country { get; set; } = Country.Belgium;

    public int? AddressId { get; set; }
    public int OrganisationId { get; set; }

    [Display(Name = "Nom complet")]
    public string FullName => $"{LastName}, {FirstName}";

    [Display(Name = "Enfants")]
    public int ChildrenCount { get; set; }

    public IEnumerable<ChildSummaryViewModel> Children { get; set; } = new List<ChildSummaryViewModel>();
}

public class ChildSummaryViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public int Age => DateTime.Today.Year - BirthDate.Year - (DateTime.Today.DayOfYear < BirthDate.DayOfYear ? 1 : 0);
}

public class ParentListViewModel
{
    public IEnumerable<ParentViewModel> Parents { get; set; } = new List<ParentViewModel>();
    public string? SearchTerm { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
}
