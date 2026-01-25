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

    public async Task<bool> IsValidMunicipalityAsync(int postalCode, string city)
    {
        return await _dbContext.BelgianMunicipalities
            .AnyAsync(m => m.PostalCode == postalCode &&
                           m.City.ToLower() == city.ToLower());
    }

    public async Task<IEnumerable<BelgianMunicipality>> SearchMunicipalitiesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<BelgianMunicipality>();
        }

        searchTerm = searchTerm.Trim();
        string lowerSearchTerm = searchTerm.ToLower();

        return await _dbContext.BelgianMunicipalities
            .Where(m => m.City.ToLower().StartsWith(lowerSearchTerm) ||
                        EF.Functions.Like(m.PostalCode.ToString(), lowerSearchTerm + "%"))
            .OrderBy(m => m.City)
            .ToListAsync();
    }

    public async Task ImportMunicipalitiesFromCsvAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Le fichier CSV n'a pas été trouvé à l'emplacement : {filePath}");
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
                if (int.TryParse(parts[0].Trim(), out int postalCode))
                {
                    string city = parts[1].Trim();

                    if (!existingMunicipalities.Any(m => m.PostalCode == postalCode && m.City.Equals(city, StringComparison.OrdinalIgnoreCase)))
                    {
                        newMunicipalities.Add(new BelgianMunicipality { PostalCode = postalCode, City = city });
                    }
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
