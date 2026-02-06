using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Account.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [EmailAddress]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Password)]
    [Display(Name = "Field.Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Field.RememberMe")]
    public bool RememberMe { get; set; }
}
