using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class ParentInformationViewModel
{
    [Required(ErrorMessage = "Le prénom est requis")]
    [StringLength(100)]
    [Display(Name = "Prénom")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(100)]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    [StringLength(200)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Format de téléphone invalide")]
    [StringLength(20)]
    [Display(Name = "Téléphone fixe")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Le numéro de GSM est requis")]
    [Phone(ErrorMessage = "Format de GSM invalide")]
    [StringLength(20)]
    [Display(Name = "GSM")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le numéro de registre national est requis")]
    [RegularExpression(@"^\d{2}\.\d{2}\.\d{2}-\d{3}\.\d{2}$", ErrorMessage = "Format invalide (ex: 85.05.12-123.45)")]
    [StringLength(15)]
    [Display(Name = "Numéro de registre national")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "La rue est requise")]
    [StringLength(200)]
    [Display(Name = "Rue et numéro")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le code postal est requis")]
    [Display(Name = "Code postal")]
    public int PostalCode { get; set; }

    [Required(ErrorMessage = "La ville est requise")]
    [StringLength(100)]
    [Display(Name = "Ville")]
    public string City { get; set; } = string.Empty;

    public int ActivityId { get; set; }
}
