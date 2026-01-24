using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Organisations.ViewModels;

public class OrganisationViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 100 caractères")]
    [Display(Name = "Nom de l'organisation")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La description est requise")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "La description doit contenir entre 10 et 500 caractères")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Logo")]
    public string? LogoUrl { get; set; }

    // Address
    [Required(ErrorMessage = "La rue est requise")]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Rue et numéro")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ville est requise")]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Ville")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le code postal est requis")]
    [Range(1000, 9999, ErrorMessage = "Code postal belge invalide")]
    [Display(Name = "Code postal")]
    public int PostalCode { get; set; }

    [Display(Name = "Pays")]
    public Country Country { get; set; } = Country.Belgium;

    public int? AddressId { get; set; }

    // Summary counts
    [Display(Name = "Nombre d'activités")]
    public int ActivitiesCount { get; set; }

    [Display(Name = "Nombre de parents")]
    public int ParentsCount { get; set; }

    [Display(Name = "Nombre de membres d'équipe")]
    public int TeamMembersCount { get; set; }

    [Display(Name = "Nombre d'utilisateurs")]
    public int UsersCount { get; set; }
}

public class OrganisationListViewModel
{
    public IEnumerable<OrganisationViewModel> Organisations { get; set; } = new List<OrganisationViewModel>();
    public string? SearchTerm { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
}
