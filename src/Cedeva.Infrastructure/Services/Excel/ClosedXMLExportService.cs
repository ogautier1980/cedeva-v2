using Cedeva.Core.Interfaces;
using ClosedXML.Excel;

namespace Cedeva.Infrastructure.Services.Excel;

public class ClosedXmlExportService : IExcelExportService
{
    public byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName, Dictionary<string, Func<T, object>> columns,
        Func<T, string>? groupSelector = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        WriteHeaders(worksheet, columns.Keys);
        WriteDataRows(worksheet, data, columns.Values, columns.Count, groupSelector);

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeaders(IXLWorksheet worksheet, IEnumerable<string> columnNames)
    {
        var colIndex = 1;
        foreach (var columnName in columnNames)
        {
            var headerCell = worksheet.Cell(1, colIndex);
            headerCell.Value = columnName;
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            colIndex++;
        }
    }

    private static void WriteDataRows<T>(IXLWorksheet worksheet, IEnumerable<T> data, IEnumerable<Func<T, object>> columnFunctions,
        int columnCount, Func<T, string>? groupSelector)
    {
        var rowIndex = 2;
        string? currentGroup = null;
        foreach (var item in data)
        {
            if (groupSelector != null)
            {
                var group = groupSelector(item);
                if (group != currentGroup)
                {
                    currentGroup = group;
                    var groupRow = worksheet.Range(rowIndex, 1, rowIndex, columnCount);
                    groupRow.Merge();
                    var groupCell = worksheet.Cell(rowIndex, 1);
                    groupCell.Value = group;
                    groupCell.Style.Font.Bold = true;
                    groupCell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE8, 0xE8, 0xE8);
                    rowIndex++;
                }
            }

            var colIndex = 1;
            foreach (var columnFunc in columnFunctions)
            {
                var value = columnFunc(item);
                var cell = worksheet.Cell(rowIndex, colIndex);
                SetCellValue(cell, value);
                colIndex++;
            }
            rowIndex++;
        }
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        if (value == null)
        {
            return;
        }

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
}
