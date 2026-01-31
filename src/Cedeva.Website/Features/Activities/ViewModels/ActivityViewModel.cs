using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Activities.ViewModels;

public class ActivityViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    [Display(Name = "Field.Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Field.IsActive")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Field.PricePerDay")]
    [Range(0, 1000)]
    [DataType(DataType.Currency)]
    public decimal? PricePerDay { get; set; }

    [Required]
    [Display(Name = "Field.StartDate")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required]
    [Display(Name = "Field.EndDate")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

    public int OrganisationId { get; set; }

    [Display(Name = "Field.Organisation")]
    public string? OrganisationName { get; set; }

    // Stats for display
    [Display(Name = "Field.BookingsCount")]
    public int BookingsCount { get; set; }

    [Display(Name = "Field.GroupsCount")]
    public int GroupsCount { get; set; }

    [Display(Name = "Field.TeamMembersCount")]
    public int TeamMembersCount { get; set; }

    // Activity days grouped by week (for Details view)
    public List<WeeklyActivityDaysViewModel> WeeklyDays { get; set; } = new();

    // For Edit: list of days with IsActive status
    public List<ActivityDayViewModel> AllDays { get; set; } = new();
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
