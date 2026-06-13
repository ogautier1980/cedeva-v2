using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Sql;

/// <summary>
/// Validates the address lookup against a real SQL Server, where the default collation is
/// case-insensitive (CI). These assertions would behave differently on SQLite (case-sensitive
/// equality), so only this suite proves the production behaviour — and that the EF queries
/// translate to SQL at all (the bug that took down the autocomplete).
/// </summary>
[Collection("Sql")]
public class MunicipalityCollationTests
{
    private readonly SqlServerFixture _fx;

    public MunicipalityCollationTests(SqlServerFixture fx) => _fx = fx;

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
    public async Task Search_IsCaseInsensitive_OnSqlServerCollation()
    {
        var sut = await SeededServiceAsync();

        // Lower-case term must still match "Gembloux" thanks to SQL Server's CI collation —
        // SQLite would NOT match this with a plain StartsWith.
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
