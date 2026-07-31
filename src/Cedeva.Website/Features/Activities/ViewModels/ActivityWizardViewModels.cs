using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Activities.ViewModels;

/// <summary>Step 1 — Titre + Dates. Creates the Activity.</summary>
public class WizardStep1ViewModel
{
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
}

/// <summary>Step 2 — Paramétrage des dates (display-only wrapper around the activity's day list).</summary>
public class WizardStep2ViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<WizardDayWeekViewModel> Weeks { get; set; } = new();
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

/// <summary>Step 3 — Règlement.</summary>
public class WizardStep3ViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.RegulationLinkUrl")]
    public string? RegulationLinkUrl { get; set; }

    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.RegulationAcceptanceText")]
    public string? RegulationAcceptanceText { get; set; }
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
}

/// <summary>Step 5 — Autres questions (reuses the existing question editor, minus the Actif toggle).</summary>
public class WizardStep5ViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<ExistingActivityQuestionViewModel> ExistingQuestions { get; set; } = new();
    public List<NewActivityQuestionViewModel> NewQuestions { get; set; } = new();
}

/// <summary>Step 6 — Affichage.</summary>
public class WizardStep6ViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Activity.PublicationStartDate")]
    public DateTime? PublicationStartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Activity.PublicationEndDate")]
    public DateTime? PublicationEndDate { get; set; }

    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.NoActiveFormMessage")]
    public string? NoActiveFormMessage { get; set; }

    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Activity.RedirectUrlAfterSubmit")]
    public string? RedirectUrlAfterSubmit { get; set; }
}
