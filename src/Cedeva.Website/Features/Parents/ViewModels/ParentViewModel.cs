using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Parents.ViewModels;

public class ParentViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le prénom est requis")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Le prénom doit contenir entre 2 et 100 caractères")]
    [Display(Name = "Prénom")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 100 caractères")]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    [StringLength(100)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Format de téléphone invalide")]
    [StringLength(100)]
    [Display(Name = "Téléphone fixe")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Le GSM est requis")]
    [Phone(ErrorMessage = "Format de GSM invalide")]
    [StringLength(100)]
    [Display(Name = "GSM")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le numéro national est requis")]
    [StringLength(15, MinimumLength = 11, ErrorMessage = "Le numéro national doit contenir entre 11 et 15 caractères")]
    [RegularExpression(@"^(\d{2})[.\- ]?(0[1-9]|1[0-2])[.\- ]?(0[1-9]|[12]\d|3[01])[.\- ]?(\d{3})[.\- ]?(\d{2})$",
        ErrorMessage = "Format du numéro national invalide")]
    [Display(Name = "Numéro national")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

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
