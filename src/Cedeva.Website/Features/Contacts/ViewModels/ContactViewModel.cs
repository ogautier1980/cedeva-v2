using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Contacts.ViewModels;

/// <summary>Create/Edit form for an "Autres contacts" entry.</summary>
public class ContactViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Validation.InvalidEmail")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Email")]
    public string? Email { get; set; }

    [StringLength(50, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.PhoneNumber")]
    public string? PhoneNumber { get; set; }

    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Contacts.Function")]
    public string? Function { get; set; }
}
