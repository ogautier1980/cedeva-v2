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
        return await _dbContext.BelgianMunicipalities
            .AnyAsync(m => m.PostalCode == postalCode &&
                           m.City.Equals(city, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<BelgianMunicipality>> SearchMunicipalitiesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<BelgianMunicipality>();
        }

        searchTerm = searchTerm.Trim();
        string lowerSearchTerm = searchTerm.ToLowerInvariant();

        // EF Core query: StartsWith(string, StringComparison) is not translatable to SQL,
        // so CA1862 doesn't apply (case-insensitivity is already handled via ToLowerInvariant).
#pragma warning disable CA1862
        return await _dbContext.BelgianMunicipalities
            .Where(m => m.City.ToLowerInvariant().StartsWith(lowerSearchTerm) ||
                        m.PostalCode.StartsWith(lowerSearchTerm))
            .OrderBy(m => m.City)
            .ToListAsync();
#pragma warning restore CA1862
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
