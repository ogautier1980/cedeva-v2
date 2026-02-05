using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Account.ViewModels;

public class ProfileViewModel
{
    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Field.Organisation")]
    public int? OrganisationId { get; set; }
}
