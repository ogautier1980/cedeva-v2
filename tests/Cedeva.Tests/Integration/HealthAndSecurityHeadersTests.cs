using System.Net;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class HealthAndSecurityHeadersTests
{
    [Fact]
    public async Task Health_IsAnonymous_AndReturnsHealthy()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task AppPages_CarrySecurityHeaders_IncludingFrameOptions()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("SAMEORIGIN");
    }

    [Fact]
    public async Task PublicRegistration_IsFramable_HasNoFrameOptionsButKeepsOtherHeaders()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Any path under /PublicRegistration goes through the middleware; a non-existent action
        // 404s without invoking controller logic, which is enough to assert the header policy.
        var response = await client.GetAsync("/PublicRegistration/DoesNotExist");

        response.Headers.Contains("X-Frame-Options").Should().BeFalse(); // framable by partner sites
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
    }
}
