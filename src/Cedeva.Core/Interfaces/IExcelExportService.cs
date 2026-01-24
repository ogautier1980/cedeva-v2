namespace Cedeva.Core.Interfaces;

public interface IExcelExportService
{
    byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName, Dictionary<string, Func<T, object>> columns);
}
