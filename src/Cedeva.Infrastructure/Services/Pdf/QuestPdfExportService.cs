using Cedeva.Core.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cedeva.Infrastructure.Services.Pdf;

public class QuestPdfExportService : IPdfExportService
{
    public QuestPdfExportService()
    {
        // Configure QuestPDF license (Community license is free for open-source projects)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ExportToPdf<T>(IEnumerable<T> data, string title, Dictionary<string, Func<T, object>> columns)
    {
        var dataList = data.ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);

                // Header
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text(title)
                            .FontSize(20)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        column.Item().Text($"Généré le {DateTime.UtcNow:dd/MM/yyyy HH:mm}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(80).AlignRight().Text($"{dataList.Count} élément(s)")
                        .FontSize(12)
                        .SemiBold();
                });

                // Content
                page.Content().PaddingVertical(10).Table(table =>
                {
                    // Define columns
                    var columnCount = columns.Count;
                    table.ColumnsDefinition(columns =>
                    {
                        for (int i = 0; i < columnCount; i++)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    // Header row
                    table.Header(header =>
                    {
                        foreach (var column in columns.Keys)
                        {
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text(column)
                                .FontColor(Colors.White)
                                .FontSize(10)
                                .Bold();
                        }
                    });

                    // Data rows
                    foreach (var item in dataList)
                    {
                        foreach (var column in columns.Values)
                        {
                            var value = column(item);
                            var displayValue = FormatValue(value);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(displayValue)
                                .FontSize(9);
                        }
                    }
                });

                // Footer
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private string FormatValue(object? value)
    {
        if (value == null)
            return string.Empty;

        return value switch
        {
            DateTime date => date.ToString("dd/MM/yyyy"),
            decimal dec => dec.ToString("N2"),
            double dbl => dbl.ToString("N2"),
            bool boolean => boolean ? "Oui" : "Non",
            _ => value.ToString() ?? string.Empty
        };
    }
}
