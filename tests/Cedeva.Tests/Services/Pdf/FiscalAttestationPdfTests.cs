using Cedeva.Core.DTOs;
using Cedeva.Infrastructure.Services.Pdf;

namespace Cedeva.Tests.Services.Pdf;

/// <summary>
/// Rules of the SPF Finances guide for form 281.86 (« Guide pour établir l'attestation pour garde
/// d'enfant »): mandatory order number, 4 periods per attestation, daily rates stated all together as
/// soon as one exceeds the ceiling.
/// </summary>
public class FiscalAttestationPdfTests
{
    private static FiscalAttestationPeriod Period(int days, decimal amount) =>
        new() { ActivityName = "Stage", From = new DateTime(2025, 7, 7), To = new DateTime(2025, 7, 11), Days = days, Amount = amount };

    private static (FiscalAttestationData Data, FiscalAttestationChild Child) Attestation(params FiscalAttestationPeriod[] periods)
    {
        var child = new FiscalAttestationChild { ChildId = 34, FirstName = "Emma", LastName = "Abras", NationalRegisterNumber = "15091112345", Periods = periods };
        var data = new FiscalAttestationData
        {
            Year = 2025,
            IssuedDate = new DateTime(2026, 2, 1),
            ParentId = 12,
            ParentFullName = "Evelyne Hautier",
            ParentNationalRegisterNumber = "85010112345",
            MaxDailyAmount = 16.90m,
            TaxCode = "1384",
            Organisation = new FiscalAttestationOrganisation { Name = "Animagique", CompanyNumber = "0123.456.789", ResponsibleName = "Hervé Gilbert", ResponsibleTitle = "Président", City = "Gembloux" },
            Children = [child]
        };
        return (data, child);
    }

    [Fact]
    public void SplitAttestations_UpToFourPeriods_OneAttestationNumberedYearParentChild()
    {
        var (data, child) = Attestation(Period(5, 50m), Period(5, 50m), Period(5, 50m), Period(5, 50m));

        var attestations = FiscalAttestationPdf.SplitAttestations(data, child);

        attestations.Should().ContainSingle();
        attestations[0].OrderNumber.Should().Be("2025-12-34");
        attestations[0].Periods.Should().HaveCount(4);
    }

    [Fact]
    public void SplitAttestations_FivePeriods_TwoAttestationsWithSuffixedNumbers()
    {
        var (data, child) = Attestation(Period(5, 50m), Period(5, 50m), Period(5, 50m), Period(5, 50m), Period(3, 30m));

        var attestations = FiscalAttestationPdf.SplitAttestations(data, child);

        attestations.Select(a => a.OrderNumber).Should().Equal("2025-12-34-1", "2025-12-34-2");
        attestations.Select(a => a.Periods.Count).Should().Equal(4, 1);
    }

    [Fact]
    public void ShowDailyRates_AllBelowTheCeiling_NoRateStated()
    {
        // 5 days for 50 € = 10 €/day, below 16.90 €.
        FiscalAttestationPdf.ShowDailyRates(16.90m, [Period(5, 50m), Period(4, 60m)]).Should().BeFalse();
    }

    [Fact]
    public void ShowDailyRates_OneAboveTheCeiling_AllRatesStated()
    {
        // 15 €/day and 20 €/day: one exceeds 16.90 € → every rate of the attestation is stated.
        FiscalAttestationPdf.ShowDailyRates(16.90m, [Period(4, 60m), Period(5, 100m)]).Should().BeTrue();
    }

    [Fact]
    public void Render_WithAllTheGuideFields_ProducesAPdf()
    {
        var (data, _) = Attestation(Period(5, 50m), Period(5, 50m), Period(5, 50m), Period(5, 50m), Period(3, 30m));

        var pdf = FiscalAttestationPdf.Render(data);

        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }
}
