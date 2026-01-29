using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Organisations.ViewModels;

public class OrganisationViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 10)]
    [Display(Name = "Field.Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Field.LogoUrl")]
    public string? LogoUrl { get; set; }

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
