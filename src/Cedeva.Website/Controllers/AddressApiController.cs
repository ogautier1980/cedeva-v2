using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cedeva.Website.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AddressApiController : ControllerBase
{
    private readonly IBelgianMunicipalityService _municipalityService;

    public AddressApiController(IBelgianMunicipalityService municipalityService)
    {
        _municipalityService = municipalityService;
    }

    [HttpGet("municipalities/search")]
    public async Task<IActionResult> SearchMunicipalities(string term)
    {
        var results = await _municipalityService.SearchMunicipalitiesAsync(term);

        return Ok(results.Select(m => new
        {
            label = $"{m.City} ({m.PostalCode})",
            value = m.City,
            postalCode = m.PostalCode
        }).ToList());
    }

    [HttpGet("validate-municipality")]
    public async Task<IActionResult> ValidateMunicipality(string city, string postalCode)
    {
        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(postalCode))
        {
            return Ok(new { isValid = false });
        }

        var isValid = await _municipalityService.IsValidMunicipalityAsync(postalCode, city);
        return Ok(new { isValid });
    }
}
