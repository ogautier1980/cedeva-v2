using Cedeva.Core.Interfaces;

namespace Cedeva.Infrastructure.Services;

/// <summary>
/// Facade service combining Excel and PDF export functionality.
/// </summary>
public class ExportFacadeService : IExportFacadeService
{
    public IExcelExportService Excel { get; }
    public IPdfExportService Pdf { get; }

    public ExportFacadeService(IExcelExportService excel, IPdfExportService pdf)
    {
        Excel = excel ?? throw new ArgumentNullException(nameof(excel));
        Pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
    }
}
