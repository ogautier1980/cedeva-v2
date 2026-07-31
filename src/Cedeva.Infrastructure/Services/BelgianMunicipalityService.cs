using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services;

public class BelgianMunicipalityService : IBelgianMunicipalityService
{
    private readonly CedevaDbContext _dbContext;

    public BelgianMunicipalityService(CedevaDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // CA1862 suggests StringComparison.OrdinalIgnoreCase overloads instead of ToLower() — but EF
    // Core cannot translate those to SQL and throws at query time (HTTP 500 on the autocomplete
    // API). ToLower() on both sides translates to SQL LOWER(), portable across SQLite (tests) and
    // PostgreSQL (prod), unlike EF.Functions.ILike (Npgsql-only). Do NOT "fix" these per CA1862.
#pragma warning disable CA1862
    public async Task<bool> IsValidMunicipalityAsync(string postalCode, string city)
    {
        var lowerCity = city.ToLower();
        return await _dbContext.BelgianMunicipalities
            .AnyAsync(m => m.PostalCode == postalCode && m.City.ToLower() == lowerCity);
    }

    public async Task<IEnumerable<BelgianMunicipality>> SearchMunicipalitiesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<BelgianMunicipality>();
        }

        var lowerTerm = searchTerm.Trim().ToLower();
        return await _dbContext.BelgianMunicipalities
            .Where(m => m.City.ToLower().StartsWith(lowerTerm) || m.PostalCode.ToLower().StartsWith(lowerTerm))
            .OrderBy(m => m.City)
            .ToListAsync();
    }
#pragma warning restore CA1862

    public async Task ImportMunicipalitiesFromCsvAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSV file not found at location: {filePath}");
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        await ImportMunicipalitiesFromCsvAsync(stream);
    }

    public async Task ImportMunicipalitiesFromCsvAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var newMunicipalities = new List<BelgianMunicipality>();

        var existingMunicipalities = await _dbContext.BelgianMunicipalities
            .AsNoTracking()
            .Select(m => new { m.PostalCode, m.City })
            .ToListAsync();

        while (await reader.ReadLineAsync() is { } line)
        {
            var parts = line.Split(';');
            if (parts.Length == 2)
            {
                var postalCode = parts[0].Trim();
                var city = parts[1].Trim();

                if (!string.IsNullOrWhiteSpace(postalCode) && !string.IsNullOrWhiteSpace(city) &&
                    !existingMunicipalities.Any(m => m.PostalCode == postalCode && m.City.Equals(city, StringComparison.OrdinalIgnoreCase)))
                {
                    newMunicipalities.Add(new BelgianMunicipality { PostalCode = postalCode, City = city });
                }
            }
        }

        if (newMunicipalities.Any())
        {
            await _dbContext.BelgianMunicipalities.AddRangeAsync(newMunicipalities);
            await _dbContext.SaveChangesAsync();
        }
    }
}
