using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.Import.ViewModels;

/// <summary>One importable entity type shown in the picker.</summary>
public class ImporterOption
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ColumnsTemplate { get; set; } = string.Empty;
    public bool AdminOnly { get; set; }
}

/// <summary>The import page: type picker, optional admin target-organisation selector, upload.</summary>
public class ImportIndexViewModel
{
    public string? SelectedType { get; set; }
    public List<ImporterOption> Importers { get; set; } = new();

    public bool IsAdmin { get; set; }

    /// <summary>For admins importing org-scoped data: the organisation the rows are written into.</summary>
    public int? TargetOrganisationId { get; set; }
    public List<SelectListItem> Organisations { get; set; } = new();

    public ImporterOption? Selected => Importers.FirstOrDefault(i => i.Key == SelectedType);
}
