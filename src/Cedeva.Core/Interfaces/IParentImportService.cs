namespace Cedeva.Core.Interfaces;

/// <summary>Outcome of a parents/children CSV import.</summary>
public class ParentImportResult
{
    public int RowsProcessed { get; set; }
    public int ParentsCreated { get; set; }
    public int ParentsReused { get; set; }
    public int ChildrenCreated { get; set; }
    public int ChildrenSkipped { get; set; }

    /// <summary>Per-row problems, formatted as "Ligne N: reason". Rows with errors are skipped.</summary>
    public List<string> Errors { get; } = new();

    public bool HasErrors => Errors.Count > 0;
}

/// <summary>
/// Imports parents (with their address) and a child per row from a CSV file. Parents are de-duplicated
/// by email within the organisation; children by national register number when present.
/// </summary>
public interface IParentImportService
{
    Task<ParentImportResult> ImportAsync(Stream csvStream, int organisationId, CancellationToken ct = default);
}
