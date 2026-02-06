using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Users.ViewModels;

public class UserViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [EmailAddress]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Field.Organisation")]
    public int? OrganisationId { get; set; }

    [Display(Name = "Field.Organisation")]
    public string OrganisationName { get; set; } = string.Empty;

    [Display(Name = "Field.Role")]
    public Role Role { get; set; }

    [Display(Name = "Field.EmailConfirmed")]
    public bool EmailConfirmed { get; set; }

    [Display(Name = "Field.IsLockedOut")]
    public bool IsLockedOut { get; set; }

    // For create/edit
    [DataType(DataType.Password)]
    [Display(Name = "Field.Password")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Field.ConfirmPassword")]
    [Compare("Password")]
    public string? ConfirmPassword { get; set; }

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    // Audit display names (for UI)
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string? ModifiedByDisplayName { get; set; }
}
