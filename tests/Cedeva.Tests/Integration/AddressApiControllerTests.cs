using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration tests for <c>AddressApiController</c> ([ApiController], route api/AddressApi).
/// Both endpoints are anonymous (no [Authorize]) and back the address autocomplete UI:
/// - GET municipalities/search?term= => JSON array of { label, value, postalCode },
///   matching City or PostalCode prefix (case-insensitive), ordered by City.
/// - GET validate-municipality?city=&postalCode= => JSON { isValid : bool }.
/// BelgianMunicipality has no OrganisationId, so no multi-tenancy filter applies.
/// </summary>
[Collection("WebApp")]
public class AddressApiControllerTests
{
    private static void SeedMunicipalities(CedevaWebApplicationFactory factory)
    {
        factory.Seed(ctx =>
        {
            ctx.AddRange(
                new BelgianMunicipality { PostalCode = "5030", City = "Gembloux" },
                new BelgianMunicipality { PostalCode = "5031", City = "Grand-Leez" },
                new BelgianMunicipality { PostalCode = "1000", City = "Bruxelles" });
            return 0;
        });
    }

    // ---- GET municipalities/search ------------------------------------------------------

    [Fact]
    public async Task SearchMunicipalities_ByCityPrefix_ReturnsMatchingMunicipality()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/AddressApi/municipalities/search?term=Gemb");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Gembloux");
        json.Should().Contain("5030");
        json.Should().Contain("Gembloux (5030)"); // label projection
        json.Should().NotContain("Bruxelles");
    }

    [Fact]
    public async Task SearchMunicipalities_IsCaseInsensitivePartial()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/AddressApi/municipalities/search?term=gembl");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Gembloux");
    }

    [Fact]
    public async Task SearchMunicipalities_ByPostalCodePrefix_ReturnsBothMatches()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // "503" prefixes both 5030 (Gembloux) and 5031 (Grand-Leez), ordered by City.
        var response = await client.GetAsync("/api/AddressApi/municipalities/search?term=503");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Gembloux");
        json.Should().Contain("Grand-Leez");
        json.Should().NotContain("Bruxelles");
        json.IndexOf("Gembloux").Should().BeLessThan(json.IndexOf("Grand-Leez")); // OrderBy(City)
    }

    [Fact]
    public async Task SearchMunicipalities_NoMatch_ReturnsEmptyArray()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/AddressApi/municipalities/search?term=Zzzz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Trim().Should().Be("[]");
    }

    [Fact]
    public async Task SearchMunicipalities_BlankTerm_IsRejectedByModelValidation()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // [ApiController] auto-validation: a whitespace-only value for the implicitly-required
        // non-nullable string 'term' fails RequiredAttribute => 400 before the action runs.
        var response = await client.GetAsync("/api/AddressApi/municipalities/search?term=%20%20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchMunicipalities_MissingTermParameter_IsRejectedByModelValidation()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // 'term' is a non-nullable string => implicitly [Required]; omitting it yields 400 (ProblemDetails).
        var response = await client.GetAsync("/api/AddressApi/municipalities/search");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchMunicipalities_IsAnonymous_NoAuthRequired()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        // Plain unauthenticated client (no X-Test-User header).
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/AddressApi/municipalities/search?term=Brux");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Bruxelles");
    }

    // ---- GET validate-municipality ------------------------------------------------------

    [Fact]
    public async Task ValidateMunicipality_MatchingPair_ReturnsIsValidTrue()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/AddressApi/validate-municipality?city=Gembloux&postalCode=5030");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"isValid\":true");
    }

    [Fact]
    public async Task ValidateMunicipality_MismatchedPair_ReturnsIsValidFalse()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // 5030 belongs to Gembloux, not Bruxelles.
        var response = await client.GetAsync("/api/AddressApi/validate-municipality?city=Bruxelles&postalCode=5030");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"isValid\":false");
    }

    [Fact]
    public async Task ValidateMunicipality_MissingCity_IsRejectedByModelValidation()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // [ApiController] auto-validation rejects the omitted non-nullable 'city' with 400 before
        // the action body (so the controller's own IsNullOrWhiteSpace guard is unreachable via the API).
        var response = await client.GetAsync("/api/AddressApi/validate-municipality?postalCode=5030");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ValidateMunicipality_MissingPostalCode_IsRejectedByModelValidation()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/AddressApi/validate-municipality?city=Gembloux");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ValidateMunicipality_BlankParameters_IsRejectedByModelValidation()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Whitespace-only values fail RequiredAttribute => 400.
        var response = await client.GetAsync("/api/AddressApi/validate-municipality?city=%20&postalCode=%20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ValidateMunicipality_IsAnonymous_NoAuthRequired()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedMunicipalities(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/AddressApi/validate-municipality?city=Bruxelles&postalCode=1000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"isValid\":true");
    }
}
