using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Users.ViewModels;

public class UserViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le prénom est requis")]
    [Display(Name = "Prénom")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est requis")]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Organisation")]
    public int? OrganisationId { get; set; }

    [Display(Name = "Organisation")]
    public string OrganisationName { get; set; } = string.Empty;

    [Display(Name = "Rôle")]
    public Role Role { get; set; }

    [Display(Name = "Email confirmé")]
    public bool EmailConfirmed { get; set; }

    [Display(Name = "Compte verrouillé")]
    public bool IsLockedOut { get; set; }

    // For create/edit
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le mot de passe")]
    [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas")]
    public string? ConfirmPassword { get; set; }
}
