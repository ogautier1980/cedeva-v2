using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class ChildInformationViewModel
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
    [DataType(DataType.Date)]
    [Display(Name = "Field.BirthDate")]
    public DateTime BirthDate { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [RegularExpression(@"^\d{2}\.\d{2}\.\d{2}-\d{3}\.\d{2}$")]
    [StringLength(15, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.NationalRegisterNumber")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Display(Name = "Field.IsDisadvantagedEnvironment")]
    public bool IsDisadvantagedEnvironment { get; set; }

    [Display(Name = "Field.IsMildDisability")]
    public bool IsMildDisability { get; set; }

    [Display(Name = "Field.IsSevereDisability")]
    public bool IsSevereDisability { get; set; }

    public int ActivityId { get; set; }
    public int ParentId { get; set; }
}
