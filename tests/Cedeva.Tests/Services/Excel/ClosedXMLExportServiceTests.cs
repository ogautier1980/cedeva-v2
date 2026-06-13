using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Services.Excel;
using ClosedXML.Excel;

namespace Cedeva.Tests.Services.Excel;

public class ClosedXmlExportServiceTests
{
    private readonly IExcelExportService _sut = new ClosedXmlExportService();

    private sealed record Row(
        string Name,
        int Quantity,
        decimal Price,
        bool Active,
        DateTime Date,
        string? Note);

    private static Dictionary<string, Func<Row, object>> Columns() => new()
    {
        ["Name"] = r => r.Name,
        ["Quantity"] = r => r.Quantity,
        ["Price"] = r => r.Price,
        ["Active"] = r => r.Active,
        ["Date"] = r => r.Date,
        ["Note"] = r => r.Note!
    };

    private static IXLWorksheet OpenSheet(byte[] bytes, string sheetName)
    {
        using var stream = new MemoryStream(bytes);
        var workbook = new XLWorkbook(stream);
        return workbook.Worksheet(sheetName);
    }

    [Fact]
    public void ExportToExcel_ReturnsNonEmptyByteArray()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), "fresh")
        };

        var bytes = _sut.ExportToExcel(data, "Items", Columns());

        bytes.Should().NotBeNull();
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void ExportToExcel_ProducesValidWorkbookWithNamedSheet()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), "fresh")
        };

        var bytes = _sut.ExportToExcel(data, "Items", Columns());

        using var stream = new MemoryStream(bytes);
        var workbook = new XLWorkbook(stream);
        workbook.Worksheets.Should().ContainSingle();
        workbook.Worksheet("Items").Should().NotBeNull();
    }

    [Fact]
    public void ExportToExcel_WritesHeadersInFirstRow()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), "fresh")
        };

        var bytes = _sut.ExportToExcel(data, "Items", Columns());
        var ws = OpenSheet(bytes, "Items");

        ws.Cell(1, 1).GetString().Should().Be("Name");
        ws.Cell(1, 2).GetString().Should().Be("Quantity");
        ws.Cell(1, 3).GetString().Should().Be("Price");
        ws.Cell(1, 4).GetString().Should().Be("Active");
        ws.Cell(1, 5).GetString().Should().Be("Date");
        ws.Cell(1, 6).GetString().Should().Be("Note");
    }

    [Fact]
    public void ExportToExcel_StylesHeaderCellsBoldAndCentered()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), "fresh")
        };

        var bytes = _sut.ExportToExcel(data, "Items", Columns());
        var ws = OpenSheet(bytes, "Items");

        var header = ws.Cell(1, 1);
        header.Style.Font.Bold.Should().BeTrue();
        header.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
    }

    [Fact]
    public void ExportToExcel_WritesDataRowsStartingAtRowTwo()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), "fresh"),
            new Row("Pear", 7, 2.25m, false, new DateTime(2026, 8, 2), "ripe")
        };

        var bytes = _sut.ExportToExcel(data, "Items", Columns());
        var ws = OpenSheet(bytes, "Items");

        ws.Cell(2, 1).GetString().Should().Be("Apple");
        ws.Cell(3, 1).GetString().Should().Be("Pear");
        // Last used row = header (1) + 2 data rows = 3
        ws.LastRowUsed()!.RowNumber().Should().Be(3);
    }

    [Fact]
    public void ExportToExcel_WritesNumericValuesAsNumbers()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), "fresh")
        };

        var bytes = _sut.ExportToExcel(data, "Items", Columns());
        var ws = OpenSheet(bytes, "Items");

        ws.Cell(2, 2).GetDouble().Should().Be(3d);     // int
        ws.Cell(2, 3).GetDouble().Should().Be(1.5d);   // decimal
    }

    [Fact]
    public void ExportToExcel_WritesDateValueAsDateTime()
    {
        var date = new DateTime(2026, 7, 1);
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, date, "fresh")
        };

        var bytes = _sut.ExportToExcel(data, "Items", Columns());
        var ws = OpenSheet(bytes, "Items");

        ws.Cell(2, 5).GetDateTime().Should().Be(date);
    }

    [Fact]
    public void ExportToExcel_WritesBoolAsOuiNon()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), "fresh"),
            new Row("Pear", 7, 2.25m, false, new DateTime(2026, 8, 2), "ripe")
        };

        var bytes = _sut.ExportToExcel(data, "Items", Columns());
        var ws = OpenSheet(bytes, "Items");

        ws.Cell(2, 4).GetString().Should().Be("Oui");
        ws.Cell(3, 4).GetString().Should().Be("Non");
    }

    [Fact]
    public void ExportToExcel_LeavesNullValueCellEmpty()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), Note: null)
        };

        var bytes = _sut.ExportToExcel(data, "Items", Columns());
        var ws = OpenSheet(bytes, "Items");

        ws.Cell(2, 6).IsEmpty().Should().BeTrue();
        ws.Cell(2, 6).GetString().Should().BeEmpty();
    }

    [Fact]
    public void ExportToExcel_EmptyData_ReturnsWorkbookWithHeadersOnly()
    {
        var data = Array.Empty<Row>();

        var bytes = _sut.ExportToExcel(data, "Items", Columns());

        bytes.Should().NotBeEmpty();

        var ws = OpenSheet(bytes, "Items");
        ws.Cell(1, 1).GetString().Should().Be("Name");
        // Only the header row is used.
        ws.LastRowUsed()!.RowNumber().Should().Be(1);
    }

    [Fact]
    public void ExportToExcel_NoColumns_ProducesEmptyButValidWorkbook()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), "fresh")
        };
        var columns = new Dictionary<string, Func<Row, object>>();

        var bytes = _sut.ExportToExcel(data, "Items", columns);

        bytes.Should().NotBeEmpty();
        var ws = OpenSheet(bytes, "Items");
        ws.LastRowUsed().Should().BeNull(); // nothing written
    }

    [Fact]
    public void ExportToExcel_PreservesColumnOrder()
    {
        var data = new[]
        {
            new Row("Apple", 3, 1.5m, true, new DateTime(2026, 7, 1), "fresh")
        };
        var columns = new Dictionary<string, Func<Row, object>>
        {
            ["First"] = r => r.Quantity,
            ["Second"] = r => r.Name
        };

        var bytes = _sut.ExportToExcel(data, "Items", columns);
        var ws = OpenSheet(bytes, "Items");

        ws.Cell(1, 1).GetString().Should().Be("First");
        ws.Cell(1, 2).GetString().Should().Be("Second");
        ws.Cell(2, 1).GetDouble().Should().Be(3d);
        ws.Cell(2, 2).GetString().Should().Be("Apple");
    }
}
