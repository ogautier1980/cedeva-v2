using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Cedeva.Website.Validation;
using Cedeva.Website.ViewModels;

namespace Cedeva.Website.Features.Activities.ViewModels;

public class ActivityViewModel : AuditableViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Field.IsActive")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Field.PricePerDay")]
    [Range(0, 1000, ErrorMessage = "Validation.Range")]
    [DataType(DataType.Currency)]
    public decimal? PricePerDay { get; set; }

    [Display(Name = "Field.ChildcarePricePerDay")]
    [Range(0, 1000, ErrorMessage = "Validation.Range")]
    [DataType(DataType.Currency)]
    public decimal? ChildcarePricePerDay { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.StartDate")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.EndDate")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

    public int OrganisationId { get; set; }

    [Display(Name = "Field.Organisation")]
    public string? OrganisationName { get; set; }

    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activities.IncludedPostalCodes")]
    public string? IncludedPostalCodes { get; set; }

    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activities.ExcludedPostalCodes")]
    public string? ExcludedPostalCodes { get; set; }

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

    // For Create: new groups and questions to be created with the activity
    public List<NewActivityGroupViewModel> NewGroups { get; set; } = new();
    public List<NewActivityQuestionViewModel> NewQuestions { get; set; } = new();

    // For Edit: existing questions with Id, DisplayOrder, IsActive
    public List<ExistingActivityQuestionViewModel> ExistingQuestions { get; set; } = new();

    // --- Signalétique (Lot J) : chaque champ, laissé vide, reprend celui de l'organisation ---

    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.DisplayTitle")]
    public string? DisplayTitle { get; set; }

    [Display(Name = "Field.Logo")]
    [AllowedExtensions(".jpg", ".jpeg", ".png", ".gif", ".svg")]
    [MaxFileSize(5 * 1024 * 1024)]
    public IFormFile? LogoFile { get; set; }

    [Display(Name = "Field.RemoveLogo")]
    public bool RemoveLogo { get; set; }

    [Display(Name = "Field.LogoUrl")]
    public string? LogoUrl { get; set; }

    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Street")]
    public string? Street { get; set; }

    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.City")]
    public string? City { get; set; }

    [StringLength(10, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.PostalCode")]
    public string? PostalCode { get; set; }

    [Display(Name = "Field.Country")]
    public Country? Country { get; set; }

    public int? AddressId { get; set; }

    [EmailAddress(ErrorMessage = "Validation.InvalidEmail")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Email")]
    public string? Email { get; set; }

    [StringLength(30, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Phone1")]
    public string? Phone1 { get; set; }

    [StringLength(30, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Phone2")]
    public string? Phone2 { get; set; }

    [StringLength(34, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.BankAccountNumber")]
    public string? BankAccountNumber { get; set; }

    [StringLength(20, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.CompanyNumber")]
    public string? CompanyNumber { get; set; }

    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.ResponsibleName")]
    public string? ResponsibleName { get; set; }

    [Display(Name = "Field.ResponsibleSignature")]
    [AllowedExtensions(".jpg", ".jpeg", ".png", ".gif", ".svg")]
    [MaxFileSize(5 * 1024 * 1024)]
    public IFormFile? ResponsibleSignatureFile { get; set; }

    [Display(Name = "Field.RemoveResponsibleSignature")]
    public bool RemoveResponsibleSignature { get; set; }

    [Display(Name = "Field.ResponsibleSignatureUrl")]
    public string? ResponsibleSignatureUrl { get; set; }
}

public class NewActivityGroupViewModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string Label { get; set; } = string.Empty;

    [Range(0, 9999, ErrorMessage = "Validation.Range")]
    public int? Capacity { get; set; }
}

public class NewActivityQuestionViewModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string QuestionText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    public QuestionType QuestionType { get; set; }

    public bool IsRequired { get; set; }

    [StringLength(1000, ErrorMessage = "Validation.StringLength")]
    public string? Options { get; set; }
}

public class ExistingActivityQuestionViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string QuestionText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    public QuestionType QuestionType { get; set; }

    public bool IsRequired { get; set; }

    [StringLength(1000, ErrorMessage = "Validation.StringLength")]
    public string? Options { get; set; }

    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;
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
