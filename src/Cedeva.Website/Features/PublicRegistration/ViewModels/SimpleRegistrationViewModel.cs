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
    [Required]
    [Display(Name = "Field.FirstName")]
    [StringLength(100)]
    public string ParentFirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Field.LastName")]
    [StringLength(100)]
    public string ParentLastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Field.Email")]
    [StringLength(255)]
    public string ParentEmail { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Field.PhoneNumber")]
    [StringLength(20)]
    public string ParentPhoneNumber { get; set; } = string.Empty;

    [Display(Name = "Field.MobilePhoneNumber")]
    [StringLength(20)]
    public string? ParentMobilePhoneNumber { get; set; }

    [Required]
    [Display(Name = "Field.Street")]
    [StringLength(200)]
    public string ParentStreet { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Field.PostalCode")]
    [StringLength(10)]
    public string ParentPostalCode { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Field.City")]
    [StringLength(100)]
    public string ParentCity { get; set; } = string.Empty;

    [Display(Name = "Field.NationalRegisterNumber")]
    [StringLength(15)]
    public string? ParentNationalRegisterNumber { get; set; }

    // Child Information
    [Required]
    [Display(Name = "Field.FirstName")]
    [StringLength(100)]
    public string ChildFirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Field.LastName")]
    [StringLength(100)]
    public string ChildLastName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Field.BirthDate")]
    public DateTime ChildBirthDate { get; set; }

    [Required]
    [Display(Name = "Field.NationalRegisterNumber")]
    [StringLength(15)]
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
