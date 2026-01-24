using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.TeamMembers.ViewModels;

public class TeamMemberViewModel
{
    public int TeamMemberId { get; set; }

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

    [Required(ErrorMessage = "La date de naissance est requise")]
    [DataType(DataType.Date)]
    [Display(Name = "Date de naissance")]
    public DateTime BirthDate { get; set; }

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

    // Team specific
    [Required(ErrorMessage = "Le rôle est requis")]
    [Display(Name = "Rôle dans l'équipe")]
    public TeamRole TeamRole { get; set; }

    [Required(ErrorMessage = "Le brevet est requis")]
    [Display(Name = "Brevet")]
    public License License { get; set; }

    [Required(ErrorMessage = "Le statut est requis")]
    [Display(Name = "Statut")]
    public Status Status { get; set; }

    [Display(Name = "Indemnité journalière")]
    [Range(0, 10000, ErrorMessage = "L'indemnité doit être entre 0 et 10000€")]
    [DataType(DataType.Currency)]
    public decimal? DailyCompensation { get; set; }

    [Required(ErrorMessage = "L'URL du brevet est requise")]
    [StringLength(100)]
    [Display(Name = "URL du brevet")]
    public string LicenseUrl { get; set; } = string.Empty;

    public int? AddressId { get; set; }
    public int OrganisationId { get; set; }

    [Display(Name = "Nom complet")]
    public string FullName => $"{LastName}, {FirstName}";

    // Summary counts
    [Display(Name = "Nombre d'activités")]
    public int ActivitiesCount { get; set; }

    [Display(Name = "Nombre de frais")]
    public int ExpensesCount { get; set; }
}
