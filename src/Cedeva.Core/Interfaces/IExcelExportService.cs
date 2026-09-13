namespace Cedeva.Core.Interfaces;

public interface IExcelExportService
{
    /// <summary>
    /// <paramref name="groupSelector"/>, when provided, inserts a labeled section row before each
    /// change in group (data must already be ordered by the same key) instead of a flat sheet.
    /// </summary>
    byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName, Dictionary<string, Func<T, object>> columns,
        Func<T, string>? groupSelector = null);
}
