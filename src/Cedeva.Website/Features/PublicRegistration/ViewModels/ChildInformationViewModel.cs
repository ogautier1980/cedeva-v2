using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class ChildInformationViewModel
{
    [Required(ErrorMessage = "Le prénom est requis")]
    [StringLength(100)]
    [Display(Name = "Prénom")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(100)]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "La date de naissance est requise")]
    [DataType(DataType.Date)]
    [Display(Name = "Date de naissance")]
    public DateTime BirthDate { get; set; }

    [Required(ErrorMessage = "Le numéro de registre national est requis")]
    [RegularExpression(@"^\d{2}\.\d{2}\.\d{2}-\d{3}\.\d{2}$", ErrorMessage = "Format invalide (ex: 15.03.10-123.45)")]
    [StringLength(15)]
    [Display(Name = "Numéro de registre national")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Display(Name = "Milieu défavorisé")]
    public bool IsDisadvantagedEnvironment { get; set; }

    [Display(Name = "Handicap léger")]
    public bool IsMildDisability { get; set; }

    [Display(Name = "Handicap lourd")]
    public bool IsSevereDisability { get; set; }

    public int ActivityId { get; set; }
    public int ParentId { get; set; }
}
