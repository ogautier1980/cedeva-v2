using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Cedeva.Website.Validation;
using Cedeva.Website.ViewModels;

namespace Cedeva.Website.Features.Organisations.ViewModels;

public class OrganisationViewModel : AuditableViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Field.Logo")]
    [AllowedExtensions(".jpg", ".jpeg", ".png", ".gif", ".svg")]
    [MaxFileSize(5 * 1024 * 1024)]
    public IFormFile? LogoFile { get; set; }

    [Display(Name = "Field.RemoveLogo")]
    public bool RemoveLogo { get; set; }

    [Display(Name = "Field.LogoUrl")]
    public string? LogoUrl { get; set; }

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

    public int? AddressId { get; set; }

    // Summary counts
    [Display(Name = "Field.ActivitiesCount")]
    public int ActivitiesCount { get; set; }

    [Display(Name = "Field.ParentsCount")]
    public int ParentsCount { get; set; }

    [Display(Name = "Field.TeamMembersCount")]
    public int TeamMembersCount { get; set; }

    [Display(Name = "Field.UsersCount")]
    public int UsersCount { get; set; }

    [Display(Name = "Field.ChildrenCount")]
    public int ChildrenCount { get; set; }
}

public class OrganisationListViewModel
{
    public IEnumerable<OrganisationViewModel> Organisations { get; set; } = new List<OrganisationViewModel>();
    public string? SearchTerm { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
}
