using System.ComponentModel.DataAnnotations;
using Cedeva.Website.Validation;
using Microsoft.AspNetCore.Http;

namespace Cedeva.Website.Features.Activities.ViewModels;

/// <summary>Step 1 — Titre + Dates. Creates the Activity, or updates it when revisited
/// (<see cref="Id"/> &gt; 0) from the "Précédent" button / progress gauge of a later step.</summary>
public class WizardStep1ViewModel
{
    /// <summary>0 = creating a new activity; &gt; 0 = updating this existing one.</summary>
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.StartDate")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.EndDate")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

    public int OrganisationId { get; set; }

    /// <summary>Étape la plus avancée déjà atteinte pour cette activité (0 en création).</summary>
    public int WizardMaxStepReached { get; set; }
}

/// <summary>Step 2 — Paramétrage des dates (display-only wrapper around the activity's day list).</summary>
public class WizardStep2ViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<WizardDayWeekViewModel> Weeks { get; set; } = new();
    public int WizardMaxStepReached { get; set; }
}

public class WizardDayWeekViewModel
{
    public int WeekNumber { get; set; }
    public List<WizardDayViewModel> Days { get; set; } = new();
}

public class WizardDayViewModel
{
    public int DayId { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool IsActive { get; set; }
    public bool IsWeekend { get; set; }
}

/// <summary>Step 3 — Règlement. The regulation document can be provided either as an external link
/// (<see cref="RegulationLinkUrl"/>) or as an uploaded PDF (<see cref="RegulationPdfFile"/>); at
/// least one must resolve to a link (an upload replaces whatever was typed in the URL field) —
/// enforced in the controller rather than via a plain [Required], since either source suffices.</summary>
public class WizardStep3ViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.RegulationLinkUrl")]
    public string? RegulationLinkUrl { get; set; }

    [AllowedExtensions(".pdf")]
    [MaxFileSize(10 * 1024 * 1024)]
    [Display(Name = "ActivityWizard.RegulationPdfFile")]
    public IFormFile? RegulationPdfFile { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.RegulationAcceptanceText")]
    public string? RegulationAcceptanceText { get; set; }

    public int WizardMaxStepReached { get; set; }
}

/// <summary>Step 4 — Limitations.</summary>
public class WizardStep4ViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activities.IncludedPostalCodes")]
    public string? IncludedPostalCodes { get; set; }

    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activities.ExcludedPostalCodes")]
    public string? ExcludedPostalCodes { get; set; }

    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.PostalCodeErrorMessage")]
    public string? PostalCodeErrorMessage { get; set; }

    [Range(1, 100000, ErrorMessage = "Validation.Range")]
    [Display(Name = "Activity.MaxChildrenPerDay")]
    public int? MaxChildrenPerDay { get; set; }

    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.FullMessage")]
    public string? FullMessage { get; set; }

    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.BirthYearQuotaExceededMessage")]
    public string? BirthYearQuotaExceededMessage { get; set; }

    public List<WizardBirthYearQuotaItem> BirthYearQuotas { get; set; } = new();
    public int WizardMaxStepReached { get; set; }
}

public class WizardBirthYearQuotaItem
{
    public int Id { get; set; }
    public int BirthYear { get; set; }
    public int MaxChildren { get; set; }
}

/// <summary>Step 5 — Autres questions (reuses the existing question editor, minus the Actif toggle).</summary>
public class WizardStep5ViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<ExistingActivityQuestionViewModel> ExistingQuestions { get; set; } = new();
    public List<NewActivityQuestionViewModel> NewQuestions { get; set; } = new();
    public int WizardMaxStepReached { get; set; }
}

/// <summary>Step 6 — Affichage.</summary>
public class WizardStep6ViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Date)]
    [Display(Name = "Activity.PublicationStartDate")]
    public DateTime? PublicationStartDate { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Date)]
    [Display(Name = "Activity.PublicationEndDate")]
    public DateTime? PublicationEndDate { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.NoActiveFormMessage")]
    public string? NoActiveFormMessage { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.RedirectUrlAfterSubmit")]
    public string? RedirectUrlAfterSubmit { get; set; }

    public int WizardMaxStepReached { get; set; }
}
