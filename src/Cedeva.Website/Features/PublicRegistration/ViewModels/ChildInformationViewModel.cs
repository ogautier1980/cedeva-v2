using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class ChildInformationViewModel
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
    [DataType(DataType.Date)]
    [Display(Name = "Field.BirthDate")]
    public DateTime BirthDate { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [RegularExpression(@"^\d{2}\.\d{2}\.\d{2}-\d{3}\.\d{2}$", ErrorMessage = "Validation.NRNFormat")]
    [StringLength(15, ErrorMessage = "Validation.StringLength")]
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
