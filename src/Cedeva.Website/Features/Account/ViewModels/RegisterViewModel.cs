using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Account.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [EmailAddress]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Field.Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Field.ConfirmPassword")]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Organisation")]
    public int OrganisationId { get; set; }
}
