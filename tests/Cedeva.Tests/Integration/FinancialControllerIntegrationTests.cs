using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class FinancialControllerIntegrationTests
{
    [Fact]
    public async Task Index_WithoutSelectedActivity_RedirectsToActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");

        var response = await client.GetAsync("/Financial");

        response.StatusCode.Should().Be(HttpStatusCode.Found); // 302
        response.Headers.Location!.ToString().Should().Contain("Activities");
    }

    [Fact]
    public async Task Index_WithActivityInOwnOrganisation_RendersDashboard()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Financier");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Financial?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stage Financier");
    }

    [Fact]
    public async Task Index_WithActivityInAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });

        // Coordinator of a different organisation must not see another org's activity.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/Financial?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

        var response = await client.GetAsync("/Financial");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
