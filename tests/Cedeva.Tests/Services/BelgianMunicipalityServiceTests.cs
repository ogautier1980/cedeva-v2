using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Services;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Services;

/// <summary>
/// Guards the address autocomplete backend. A prior version used
/// m.City.ToLowerInvariant().StartsWith(...) and string.Equals(.., StringComparison)
/// inside the EF query, which EF Core cannot translate to SQL — the queries threw at
/// runtime (HTTP 500 on /api/AddressApi). These tests exercise the real EF query
/// pipeline (SQLite) so an untranslatable expression fails the build, not production.
/// </summary>
public class BelgianMunicipalityServiceTests
{
    private static SqliteTestContext SeedMunicipalities()
    {
        var db = new SqliteTestContext();
        db.Context.AddRange(
            new BelgianMunicipality { PostalCode = "5030", City = "Gembloux" },
            new BelgianMunicipality { PostalCode = "5030", City = "Beuzet" },
            new BelgianMunicipality { PostalCode = "1000", City = "Bruxelles" });
        db.Context.SaveChanges();
        return db;
    }

    private static BelgianMunicipalityService Sut(SqliteTestContext db) => new(db.NewContext());

    [Fact]
    public async Task SearchMunicipalities_ByPostalCode_ReturnsMatches()
    {
        using var db = SeedMunicipalities();

        var results = (await Sut(db).SearchMunicipalitiesAsync("5030")).ToList();

        results.Should().HaveCount(2);
        results.Should().OnlyContain(m => m.PostalCode == "5030");
    }

    [Fact]
    public async Task SearchMunicipalities_ByCityName_IsCaseInsensitive()
    {
        using var db = SeedMunicipalities();

        var results = (await Sut(db).SearchMunicipalitiesAsync("gembl")).ToList();

        results.Should().ContainSingle(m => m.City == "Gembloux");
    }

    [Fact]
    public async Task SearchMunicipalities_BlankTerm_ReturnsEmpty()
    {
        using var db = SeedMunicipalities();

        var results = await Sut(db).SearchMunicipalitiesAsync("   ");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task IsValidMunicipality_MatchingPair_ReturnsTrue()
    {
        using var db = SeedMunicipalities();

        (await Sut(db).IsValidMunicipalityAsync("5030", "Gembloux")).Should().BeTrue();
    }

    [Fact]
    public async Task IsValidMunicipality_MismatchedPair_ReturnsFalse()
    {
        using var db = SeedMunicipalities();

        (await Sut(db).IsValidMunicipalityAsync("1000", "Gembloux")).Should().BeFalse();
    }

    [Fact]
    public async Task ImportFromCsv_AddsNewRows_SkipsDuplicatesAndMalformedLines()
    {
        using var db = SeedMunicipalities(); // already has 1000 Bruxelles, 5030 Gembloux/Beuzet
        var csv =
            "4000;Liège\n" +        // new
            "2000;Antwerpen\n" +    // new
            "1000;Bruxelles\n" +    // duplicate -> skipped
            "1000;BRUXELLES\n" +    // duplicate (case-insensitive) -> skipped
            "  ;Ville\n" +          // blank postal code -> skipped
            "malformed-line\n" +    // not 2 columns -> skipped
            "\n";                   // empty -> skipped

        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        await Sut(db).ImportMunicipalitiesFromCsvAsync(stream);

        using var verify = db.NewContext();
        verify.BelgianMunicipalities.Should().HaveCount(5, "only the 2 genuinely new municipalities are added to the original 3");
        verify.BelgianMunicipalities.Should().Contain(m => m.PostalCode == "4000" && m.City == "Liège");
        verify.BelgianMunicipalities.Should().Contain(m => m.PostalCode == "2000" && m.City == "Antwerpen");
        verify.BelgianMunicipalities.Count(m => m.City.ToUpper() == "BRUXELLES").Should().Be(1, "the case-insensitive duplicate must not be inserted");
    }

    [Fact]
    public async Task ImportFromCsv_EmptyStream_AddsNothing()
    {
        using var db = SeedMunicipalities();

        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(""));
        await Sut(db).ImportMunicipalitiesFromCsvAsync(stream);

        using var verify = db.NewContext();
        verify.BelgianMunicipalities.Should().HaveCount(3, "an empty CSV leaves the table unchanged");
    }
}
