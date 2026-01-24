using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Children.ViewModels;

public class ChildViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le prénom est obligatoire")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Le prénom doit contenir entre 2 et 100 caractères")]
    [Display(Name = "Prénom")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est obligatoire")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 100 caractères")]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le numéro de registre national est obligatoire")]
    [StringLength(15, MinimumLength = 11, ErrorMessage = "Le numéro de registre national doit contenir entre 11 et 15 caractères")]
    [RegularExpression(@"^(\d{2})[.\- ]?(0[1-9]|1[0-2])[.\- ]?(0[1-9]|[12]\d|3[01])[.\- ]?(\d{3})[.\- ]?(\d{2})$",
        ErrorMessage = "Format du numéro de registre national invalide")]
    [Display(Name = "Numéro de registre national")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "La date de naissance est obligatoire")]
    [DataType(DataType.Date)]
    [Display(Name = "Date de naissance")]
    public DateTime BirthDate { get; set; }

    [Display(Name = "Environnement défavorisé")]
    public bool IsDisadvantagedEnvironment { get; set; }

    [Display(Name = "Handicap léger")]
    public bool IsMildDisability { get; set; }

    [Display(Name = "Handicap sévère")]
    public bool IsSevereDisability { get; set; }

    [Required(ErrorMessage = "Le parent est obligatoire")]
    [Display(Name = "Parent")]
    public int ParentId { get; set; }

    [Display(Name = "Groupe d'activité")]
    public int? ActivityGroupId { get; set; }

    // Navigation properties for display
    public string? ParentFullName { get; set; }
    public string? ActivityGroupName { get; set; }
    public List<BookingSummaryViewModel> Bookings { get; set; } = new();
}

public class BookingSummaryViewModel
{
    public int Id { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsConfirmed { get; set; }
}
