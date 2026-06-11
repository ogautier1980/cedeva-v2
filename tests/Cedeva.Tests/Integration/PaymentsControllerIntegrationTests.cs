using System.Net;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class PaymentsControllerIntegrationTests
{
    [Fact]
    public async Task Index_AsCoordinator_RendersPaymentsPage()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.GetAsync("/Payments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Payments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
