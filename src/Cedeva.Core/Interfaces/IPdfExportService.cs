namespace Cedeva.Core.Interfaces;

public interface IPdfExportService
{
    /// <summary>
    /// <paramref name="groupSelector"/>, when provided, inserts a labeled section row before each
    /// change in group (data must already be ordered by the same key) instead of a flat table.
    /// </summary>
    byte[] ExportToPdf<T>(IEnumerable<T> data, string title, Dictionary<string, Func<T, object>> columns,
        Func<T, string>? groupSelector = null);
}
