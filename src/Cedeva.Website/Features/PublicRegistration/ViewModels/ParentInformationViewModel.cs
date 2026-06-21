using System.ComponentModel.DataAnnotations;
using Cedeva.Website.Validation;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class ParentInformationViewModel
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
    [EmailAddress(ErrorMessage = "Validation.InvalidEmail")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [RegularExpression(@"^((\+32|0032)[\s./-]?|0)[\s./-]?\d([\s./-]?\d){7,8}$", ErrorMessage = "Validation.InvalidPhoneNumber")]
    [StringLength(20, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.PhoneNumber")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [RegularExpression(@"^((\+32|0032)[\s./-]?|0)[\s./-]?\d([\s./-]?\d){7,8}$", ErrorMessage = "Validation.InvalidPhoneNumber")]
    [StringLength(20, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.MobilePhoneNumber")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(15, ErrorMessage = "Validation.StringLength")]
    [ValidNationalRegisterNumber]
    [Display(Name = "Field.NationalRegisterNumber")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Street")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(10, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.PostalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.City")]
    public string City { get; set; } = string.Empty;

    public int ActivityId { get; set; }
}
