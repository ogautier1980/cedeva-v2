using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Account.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [EmailAddress]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Validation.StringLength")]
    [DataType(DataType.Password)]
    [Display(Name = "Field.Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Password)]
    [Display(Name = "Field.ConfirmPassword")]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.Organisation")]
    public int OrganisationId { get; set; }
}
