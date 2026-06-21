using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.EmailTemplates.ViewModels;

/// <summary>Picker for importing another activity's templates into the current activity.</summary>
public class ImportTemplatesViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;

    public int SourceActivityId { get; set; }
    public List<SelectListItem> Sources { get; set; } = new();
}
