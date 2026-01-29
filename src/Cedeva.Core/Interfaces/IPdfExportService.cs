namespace Cedeva.Core.Interfaces;

public interface IPdfExportService
{
    byte[] ExportToPdf<T>(IEnumerable<T> data, string title, Dictionary<string, Func<T, object>> columns);
}
