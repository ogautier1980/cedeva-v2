using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class ParentInformationViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(200)]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(20)]
    [Display(Name = "Field.PhoneNumber")]
    public string? PhoneNumber { get; set; }

    [Required]
    [Phone]
    [StringLength(20)]
    [Display(Name = "Field.MobilePhoneNumber")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{2}\.\d{2}\.\d{2}-\d{3}\.\d{2}$")]
    [StringLength(15)]
    [Display(Name = "Field.NationalRegisterNumber")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Field.Street")]
    public string Street { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    [Display(Name = "Field.PostalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Field.City")]
    public string City { get; set; } = string.Empty;

    public int ActivityId { get; set; }
}
