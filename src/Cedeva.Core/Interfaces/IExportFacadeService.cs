namespace Cedeva.Core.Interfaces;

/// <summary>
/// Facade service combining Excel and PDF export functionality.
/// Reduces constructor parameter count in controllers.
/// </summary>
public interface IExportFacadeService
{
    /// <summary>
    /// Excel export service.
    /// </summary>
    IExcelExportService Excel { get; }

    /// <summary>
    /// PDF export service.
    /// </summary>
    IPdfExportService Pdf { get; }
}
