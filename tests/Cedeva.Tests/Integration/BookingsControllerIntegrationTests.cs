using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class BookingsControllerIntegrationTests
{
    [Fact]
    public async Task ProtectedEndpoint_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0); // create schema

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Bookings/GetGroupsByActivity?activityId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetGroupsByActivity_AuthenticatedUser_ReturnsActivityGroupsAsJson()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            var g1 = TestData.Group(a, "Alpha");
            var g2 = TestData.Group(a, "Beta");
            ctx.AddRange(org, a, g1, g2);
            return a;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync($"/Bookings/GetGroupsByActivity?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Alpha").And.Contain("Beta");
    }

    [Fact]
    public async Task GetActivityDays_ReturnsOnlyActiveDaysAsJson()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            a.Days.Add(new ActivityDay { Label = "JourActif", DayDate = new DateTime(2026, 7, 6), IsActive = true });
            a.Days.Add(new ActivityDay { Label = "JourInactif", DayDate = new DateTime(2026, 7, 7), IsActive = false });
            ctx.AddRange(org, a);
            return a;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync($"/Bookings/GetActivityDays?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("JourActif").And.NotContain("JourInactif"); // IsActive filter
    }
}
