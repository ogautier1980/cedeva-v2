using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class ParentInformationViewModel
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
    [StringLength(200, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(20, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.PhoneNumber")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Phone]
    [StringLength(20, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.MobilePhoneNumber")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [RegularExpression(@"^\d{2}\.\d{2}\.\d{2}-\d{3}\.\d{2}$")]
    [StringLength(15, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.NationalRegisterNumber")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(200, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Street")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(10, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.PostalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.City")]
    public string City { get; set; } = string.Empty;

    public int ActivityId { get; set; }
}
