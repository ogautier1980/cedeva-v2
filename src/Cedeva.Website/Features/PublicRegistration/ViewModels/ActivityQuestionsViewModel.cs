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
}
