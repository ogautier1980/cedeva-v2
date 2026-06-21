namespace Cedeva.Core.Interfaces;

/// <summary>Outcome of a CSV import (any entity type).</summary>
public class CsvImportResult
{
    public int RowsProcessed { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }

    /// <summary>Extra counters keyed by a localization key (e.g. "Import.ChildrenCreated" -> 5).</summary>
    public Dictionary<string, int> Extra { get; } = new();

    /// <summary>Per-row problems, formatted as "Ligne N: reason". Rows with errors are skipped.</summary>
    public List<string> Errors { get; } = new();

    public bool HasErrors => Errors.Count > 0;

    public void AddExtra(string localizationKey, int count = 1) =>
        Extra[localizationKey] = Extra.GetValueOrDefault(localizationKey) + count;
}

/// <summary>
/// Imports one entity type from a CSV file. Implementations are tenancy-safe: the target
/// organisation is supplied by the caller (the logged-in user's organisation, or an admin-selected
/// one) and a CSV must never be able to write into a different organisation.
/// </summary>
public interface ICsvEntityImporter
{
    /// <summary>Stable key used in routes and the type picker (e.g. "parents", "teammembers").</summary>
    string Key { get; }

    /// <summary>Localization key for the human-readable type name.</summary>
    string DisplayNameKey { get; }

    /// <summary>Example header row shown to the user.</summary>
    string ColumnsTemplate { get; }

    /// <summary>
    /// When true, this importer creates organisation-level/cross-tenant data and is restricted to
    /// admins (e.g. importing organisations). When false, it writes into a single organisation.
    /// </summary>
    bool AdminOnly { get; }

    /// <param name="organisationId">
    /// Target organisation for org-scoped imports (ignored by <see cref="AdminOnly"/> importers).
    /// </param>
    Task<CsvImportResult> ImportAsync(Stream csvStream, int organisationId, CancellationToken ct = default);
}
