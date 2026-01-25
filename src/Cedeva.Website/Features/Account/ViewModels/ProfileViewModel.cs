using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Account.ViewModels;

public class ProfileViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Field.Organisation")]
    public int? OrganisationId { get; set; }
}
