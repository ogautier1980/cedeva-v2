using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Parents.ViewModels;

public class ParentViewModel
{
    public int Id { get; set; }

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

    [Phone]
    [StringLength(100)]
    [Display(Name = "Field.LandlineNumber")]
    public string? PhoneNumber { get; set; }

    [Required]
    [Phone]
    [StringLength(100)]
    [Display(Name = "Field.MobilePhoneNumber")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(15, MinimumLength = 11)]
    [RegularExpression(@"^(\d{2})[.\- ]?(0[1-9]|1[0-2])[.\- ]?(0[1-9]|[12]\d|3[01])[.\- ]?(\d{3})[.\- ]?(\d{2})$")]
    [Display(Name = "Field.NationalRegisterNumber")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

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
    [Range(1000, 9999)]
    [Display(Name = "Field.PostalCode")]
    public int PostalCode { get; set; }

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
