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

    public async Task<bool> IsValidMunicipalityAsync(string postalCode, string city)
    {
        // Case-insensitivity is handled server-side by SQL Server's default CI collation.
        // EF Core cannot translate string.Equals(string, StringComparison) to SQL.
        return await _dbContext.BelgianMunicipalities
            .AnyAsync(m => m.PostalCode == postalCode && m.City == city);
    }

    public async Task<IEnumerable<BelgianMunicipality>> SearchMunicipalitiesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<BelgianMunicipality>();
        }

        searchTerm = searchTerm.Trim();

        // Plain StartsWith translates to SQL LIKE 'term%' and is case-insensitive under
        // SQL Server's default CI collation. Do NOT use ToLowerInvariant() here: EF Core
        // cannot translate it and throws at query time (HTTP 500 on the autocomplete API).
        return await _dbContext.BelgianMunicipalities
            .Where(m => m.City.StartsWith(searchTerm) || m.PostalCode.StartsWith(searchTerm))
            .OrderBy(m => m.City)
            .ToListAsync();
    }

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
