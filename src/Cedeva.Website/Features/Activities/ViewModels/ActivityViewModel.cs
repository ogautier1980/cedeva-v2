using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Activities.ViewModels;

public class ActivityViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(100, ErrorMessage = "Le nom ne peut pas dépasser 100 caractères")]
    [Display(Name = "Nom")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La description est requise")]
    [StringLength(500, ErrorMessage = "La description ne peut pas dépasser 500 caractères")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Prix par jour (€)")]
    [Range(0, 1000, ErrorMessage = "Le prix doit être entre 0 et 1000€")]
    [DataType(DataType.Currency)]
    public decimal? PricePerDay { get; set; }

    [Required(ErrorMessage = "La date de début est requise")]
    [Display(Name = "Date de début")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "La date de fin est requise")]
    [Display(Name = "Date de fin")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

    public int OrganisationId { get; set; }

    [Display(Name = "Organisation")]
    public string? OrganisationName { get; set; }

    // Stats for display
    [Display(Name = "Inscriptions")]
    public int BookingsCount { get; set; }

    [Display(Name = "Groupes")]
    public int GroupsCount { get; set; }

    [Display(Name = "Équipe")]
    public int TeamMembersCount { get; set; }
}

public class ActivityListViewModel
{
    public IEnumerable<ActivityViewModel> Activities { get; set; } = new List<ActivityViewModel>();
    public string? SearchTerm { get; set; }
    public bool? ShowActiveOnly { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
}
