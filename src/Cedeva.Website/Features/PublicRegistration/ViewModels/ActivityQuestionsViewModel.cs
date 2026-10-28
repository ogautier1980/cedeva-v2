using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class ActivityQuestionsViewModel
{
    public int ActivityId { get; set; }
    public int ParentId { get; set; }
    public int ChildId { get; set; }

    public List<ActivityQuestion> Questions { get; set; } = new();
    public Dictionary<int, string> Answers { get; set; } = new();

    public string? RegulationLinkUrl { get; set; }
    public string? RegulationAcceptanceText { get; set; }
    public bool AcceptRegulation { get; set; }

    /// <summary>Weeks selected by the parent (week numbers). See <see cref="ActivityWeekOption"/>.</summary>
    public List<int> SelectedWeeks { get; set; } = new();

    /// <summary>Days ticked when the activity allows day-by-day registration (instead of weeks).</summary>
    public List<int> SelectedDays { get; set; } = new();

    public bool AllowDayByDayRegistration { get; set; }

    public decimal? PricePerDay { get; set; }
}
