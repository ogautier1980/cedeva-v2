using Cedeva.Core.Entities;

namespace Cedeva.Core.Interfaces;

public interface IBelgianMunicipalityService
{
    Task<bool> IsValidMunicipalityAsync(string postalCode, string city);
    Task<IEnumerable<BelgianMunicipality>> SearchMunicipalitiesAsync(string searchTerm);
    Task ImportMunicipalitiesFromCsvAsync(string filePath);
    Task ImportMunicipalitiesFromCsvAsync(Stream stream);
}
