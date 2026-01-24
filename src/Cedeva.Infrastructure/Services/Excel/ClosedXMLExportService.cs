using Cedeva.Core.Interfaces;
using ClosedXML.Excel;

namespace Cedeva.Infrastructure.Services.Excel;

public class ClosedXMLExportService : IExcelExportService
{
    public byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName, Dictionary<string, Func<T, object>> columns)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Write headers
        var colIndex = 1;
        foreach (var column in columns.Keys)
        {
            var headerCell = worksheet.Cell(1, colIndex);
            headerCell.Value = column;
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            colIndex++;
        }

        // Write data
        var rowIndex = 2;
        foreach (var item in data)
        {
            colIndex = 1;
            foreach (var columnFunc in columns.Values)
            {
                var value = columnFunc(item);
                var cell = worksheet.Cell(rowIndex, colIndex);

                if (value != null)
                {
                    if (value is DateTime dateTime)
                    {
                        cell.Value = dateTime;
                        cell.Style.DateFormat.Format = "dd/mm/yyyy";
                    }
                    else if (value is decimal || value is double || value is float)
                    {
                        cell.Value = Convert.ToDouble(value);
                        cell.Style.NumberFormat.Format = "#,##0.00";
                    }
                    else if (value is int || value is long)
                    {
                        cell.Value = Convert.ToInt64(value);
                    }
                    else if (value is bool boolValue)
                    {
                        cell.Value = boolValue ? "Oui" : "Non";
                    }
                    else
                    {
                        cell.Value = value.ToString();
                    }
                }

                colIndex++;
            }
            rowIndex++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Return as byte array
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
