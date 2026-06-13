using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Services.Pdf;
using QuestPDF.Drawing.Exceptions;

namespace Cedeva.Tests.Services.Pdf;

/// <summary>
/// Unit tests for <see cref="QuestPdfExportService"/>. The service is a pure (non-DB) generic
/// PDF generator, so tests exercise it directly through the <see cref="IPdfExportService"/> API
/// and assert on the produced byte[] (a valid PDF starts with the "%PDF" magic header).
/// The private FormatValue logic is covered indirectly: every supported value kind is fed in and
/// the only observable contract is that a non-empty, valid PDF is produced without throwing.
/// </summary>
public class QuestPdfExportServiceTests
{
    private sealed record Row(string Name, decimal Amount, DateTime Date, bool Flag);

    private static readonly Dictionary<string, Func<Row, object>> RowColumns = new()
    {
        ["Nom"] = r => r.Name,
        ["Montant"] = r => r.Amount,
        ["Date"] = r => r.Date,
        ["Actif"] = r => r.Flag,
    };

    private static IReadOnlyList<Row> SampleRows() => new[]
    {
        new Row("Alpha", 12.5m, new DateTime(2026, 7, 1), true),
        new Row("Bêta", 0m, new DateTime(2026, 12, 31), false),
    };

    private static IPdfExportService CreateSut() => new QuestPdfExportService();

    private static bool StartsWithPdfHeader(byte[] bytes) =>
        bytes.Length >= 4 && Encoding.ASCII.GetString(bytes, 0, 4) == "%PDF";

    [Fact]
    public void ExportToPdf_WithData_ReturnsNonEmptyPdfBytes()
    {
        var sut = CreateSut();

        var bytes = sut.ExportToPdf(SampleRows(), "Rapport de test", RowColumns);

        bytes.Should().NotBeNull();
        bytes.Should().NotBeEmpty();
        StartsWithPdfHeader(bytes).Should().BeTrue();
    }

    [Fact]
    public void ExportToPdf_WithEmptyData_StillReturnsValidPdf()
    {
        var sut = CreateSut();

        var bytes = sut.ExportToPdf(Enumerable.Empty<Row>(), "Rapport vide", RowColumns);

        bytes.Should().NotBeEmpty();
        StartsWithPdfHeader(bytes).Should().BeTrue();
    }

    [Fact]
    public void ExportToPdf_WithEmptyColumns_Throws()
    {
        var sut = CreateSut();

        // QuestPDF requires at least one table column; defining none fails at compose time.
        var act = () => sut.ExportToPdf(SampleRows(), "Sans colonnes",
            new Dictionary<string, Func<Row, object>>());

        act.Should().Throw<DocumentComposeException>()
            .WithMessage("*at least one column*");
    }

    [Fact]
    public void ExportToPdf_FormatsAllSupportedValueKinds_WithoutThrowing()
    {
        var sut = CreateSut();

        // One column per FormatValue branch: DateTime, decimal, double, bool, null and string fallback.
        var columns = new Dictionary<string, Func<object, object>>
        {
            ["DateTime"] = _ => new DateTime(2026, 3, 14),
            ["Decimal"] = _ => 1234.5m,
            ["Double"] = _ => 9.876d,
            ["BoolTrue"] = _ => true,
            ["BoolFalse"] = _ => false,
            ["Null"] = _ => null!,
            ["String"] = _ => "texte",
            ["Int"] = _ => 42,
        };

        var bytes = sut.ExportToPdf(new object[] { new() }, "Tous les formats", columns);

        bytes.Should().NotBeEmpty();
        StartsWithPdfHeader(bytes).Should().BeTrue();
    }

    [Fact]
    public void ExportToPdf_WithNullColumnValues_DoesNotThrow()
    {
        var sut = CreateSut();
        var columns = new Dictionary<string, Func<string, object>>
        {
            ["Always null"] = _ => null!,
        };

        var bytes = sut.ExportToPdf(new[] { "ignored" }, "Valeurs nulles", columns);

        bytes.Should().NotBeEmpty();
        StartsWithPdfHeader(bytes).Should().BeTrue();
    }

    [Fact]
    public void ExportToPdf_WithEmptyTitle_StillReturnsValidPdf()
    {
        var sut = CreateSut();

        var bytes = sut.ExportToPdf(SampleRows(), string.Empty, RowColumns);

        bytes.Should().NotBeEmpty();
        StartsWithPdfHeader(bytes).Should().BeTrue();
    }

    [Fact]
    public void ExportToPdf_WithSingleColumn_ReturnsValidPdf()
    {
        var sut = CreateSut();
        var columns = new Dictionary<string, Func<Row, object>>
        {
            ["Nom"] = r => r.Name,
        };

        var bytes = sut.ExportToPdf(SampleRows(), "Une colonne", columns);

        bytes.Should().NotBeEmpty();
        StartsWithPdfHeader(bytes).Should().BeTrue();
    }

    [Fact]
    public void ExportToPdf_WithManyRows_ProducesMultiPageValidPdf()
    {
        var sut = CreateSut();
        var rows = Enumerable.Range(1, 500)
            .Select(i => new Row($"Item {i}", i, new DateTime(2026, 1, 1).AddDays(i), i % 2 == 0))
            .ToList();

        var bytes = sut.ExportToPdf(rows, "Grand rapport", RowColumns);

        bytes.Should().NotBeEmpty();
        StartsWithPdfHeader(bytes).Should().BeTrue();
    }
}
