using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class SimpleRegistrationViewModel
{
    public int ActivityId { get; set; }
    public string? ActivityName { get; set; }
    public string? ActivityDescription { get; set; }
    public DateTime? ActivityStartDate { get; set; }
    public DateTime? ActivityEndDate { get; set; }
    public decimal? PricePerDay { get; set; }

    // Parent Information
    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.FirstName")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string ParentFirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.LastName")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string ParentLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [EmailAddress]
    [Display(Name = "Field.Email")]
    [StringLength(255, ErrorMessage = "Validation.StringLength")]
    public string ParentEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Phone]
    [Display(Name = "Field.PhoneNumber")]
    [StringLength(20, ErrorMessage = "Validation.StringLength")]
    public string ParentPhoneNumber { get; set; } = string.Empty;

    [Display(Name = "Field.MobilePhoneNumber")]
    [StringLength(20, ErrorMessage = "Validation.StringLength")]
    public string? ParentMobilePhoneNumber { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.Street")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    public string ParentStreet { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.PostalCode")]
    [StringLength(10, ErrorMessage = "Validation.StringLength")]
    public string ParentPostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.City")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string ParentCity { get; set; } = string.Empty;

    [Display(Name = "Field.NationalRegisterNumber")]
    [StringLength(15, ErrorMessage = "Validation.StringLength")]
    public string? ParentNationalRegisterNumber { get; set; }

    // Child Information
    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.FirstName")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string ChildFirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.LastName")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string ChildLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.BirthDate")]
    public DateTime ChildBirthDate { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.NationalRegisterNumber")]
    [StringLength(15, ErrorMessage = "Validation.StringLength")]
    public string ChildNationalRegisterNumber { get; set; } = string.Empty;

    [Display(Name = "Field.IsDisadvantagedEnvironment")]
    public bool IsDisadvantagedEnvironment { get; set; }

    [Display(Name = "Field.IsMildDisability")]
    public bool IsMildDisability { get; set; }

    [Display(Name = "Field.IsSevereDisability")]
    public bool IsSevereDisability { get; set; }

    // Custom questions
    public Dictionary<int, string> QuestionAnswers { get; set; } = new();
}
