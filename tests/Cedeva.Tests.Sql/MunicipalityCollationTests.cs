using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Sql;

/// <summary>
/// Validates the address lookup against a real PostgreSQL, which is case-sensitive by default
/// (unlike SQL Server's default CI collation). These assertions prove that
/// <see cref="BelgianMunicipalityService"/> compensates explicitly via portable ToLower()
/// comparisons — and that the EF queries translate to SQL and execute correctly against the real
/// driver/migration chain (the bug class that took down the autocomplete).
/// </summary>
[Collection("Sql")]
public class MunicipalityCollationTests
{
    private readonly PostgreSqlFixture _fx;

    public MunicipalityCollationTests(PostgreSqlFixture fx) => _fx = fx;

    private async Task<BelgianMunicipalityService> SeededServiceAsync()
    {
        await using var seed = _fx.NewContext();
        if (!await seed.BelgianMunicipalities.AnyAsync())
        {
            seed.AddRange(
                new BelgianMunicipality { PostalCode = "5030", City = "Gembloux" },
                new BelgianMunicipality { PostalCode = "1000", City = "Bruxelles" });
            await seed.SaveChangesAsync();
        }
        return new BelgianMunicipalityService(_fx.NewContext());
    }

    [Fact]
    public async Task Search_ByPartialCity_TranslatesAndReturnsMatch()
    {
        var sut = await SeededServiceAsync();

        var results = (await sut.SearchMunicipalitiesAsync("Gembl")).ToList();

        results.Should().ContainSingle(m => m.City == "Gembloux");
    }

    [Fact]
    public async Task Search_IsCaseInsensitive_ViaToLower()
    {
        var sut = await SeededServiceAsync();

        // Upper-case term must still match "Gembloux" thanks to the ToLower() comparison — a plain
        // StartsWith would NOT match this on PostgreSQL's case-sensitive default collation.
        var results = (await sut.SearchMunicipalitiesAsync("GEMBL")).ToList();

        results.Should().ContainSingle(m => m.City == "Gembloux");
    }

    [Fact]
    public async Task IsValid_MatchesRegardlessOfCase()
    {
        var sut = await SeededServiceAsync();

        (await sut.IsValidMunicipalityAsync("5030", "gembloux")).Should().BeTrue();
        (await sut.IsValidMunicipalityAsync("5030", "Gembloux")).Should().BeTrue();
        (await sut.IsValidMunicipalityAsync("5030", "Bruxelles")).Should().BeFalse();
    }
}
