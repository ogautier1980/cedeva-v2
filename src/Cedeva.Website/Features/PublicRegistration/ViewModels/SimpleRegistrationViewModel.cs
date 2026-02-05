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
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.FirstName")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string ParentFirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.LastName")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string ParentLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [EmailAddress]
    [Display(Name = "Field.Email")]
    [StringLength(255, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string ParentEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Phone]
    [Display(Name = "Field.PhoneNumber")]
    [StringLength(20, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string ParentPhoneNumber { get; set; } = string.Empty;

    [Display(Name = "Field.MobilePhoneNumber")]
    [StringLength(20, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? ParentMobilePhoneNumber { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Street")]
    [StringLength(200, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string ParentStreet { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.PostalCode")]
    [StringLength(10, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string ParentPostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.City")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string ParentCity { get; set; } = string.Empty;

    [Display(Name = "Field.NationalRegisterNumber")]
    [StringLength(15, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? ParentNationalRegisterNumber { get; set; }

    // Child Information
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.FirstName")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string ChildFirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.LastName")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string ChildLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.BirthDate")]
    public DateTime ChildBirthDate { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.NationalRegisterNumber")]
    [StringLength(15, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
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
