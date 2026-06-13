using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration tests for <c>HomeController</c>:
/// - GET / (Index dashboard) requires auth and renders organisation-scoped stats,
/// - POST /Home/SetLanguage ([AllowAnonymous]) sets the culture cookie and local-redirects,
/// - GET /Home/Error ([AllowAnonymous]) renders the error view.
/// </summary>
[Collection("WebApp")]
public class HomeControllerIntegrationTests
{
    // ---- GET / (Index dashboard) --------------------------------------------------------

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0); // create schema

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Index_AuthenticatedCoordinator_RendersDashboard()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // The dashboard view always renders the quick-actions card with this link.
        html.Should().Contain("/Activities/Create");
    }

    [Fact]
    public async Task Index_RendersStatsForOwnOrganisationData()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var activity = TestData.Activity(org, "Stage Accueil");
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            ctx.AddRange(org, parent, child, activity, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // Recent activities / bookings sections surface the seeded data.
        html.Should().Contain("Stage Accueil");
        html.Should().Contain("Enfant"); // child last name in recent bookings (FirstName is HTML-encoded)
    }

    [Fact]
    public async Task Index_CoordinatorOfOtherOrganisation_DoesNotSeeForeignActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var orgA = TestData.Organisation("Org A");
            var activity = TestData.Activity(orgA, "Stage Reserve Org A");
            ctx.AddRange(orgA, activity);
            return 0;
        });

        // Coordinator belonging to a different organisation: query filters hide org A's data.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("Stage Reserve Org A");
    }

    [Fact]
    public async Task Index_Admin_SeesAllOrganisationsActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var orgA = TestData.Organisation("Org A");
            var orgB = TestData.Organisation("Org B");
            var a1 = TestData.Activity(orgA, "Stage Admin OrgA");
            var a2 = TestData.Activity(orgB, "Stage Admin OrgB");
            ctx.AddRange(orgA, orgB, a1, a2);
            return 0;
        });

        // Admin bypasses tenant filters, so both organisations' activities are visible.
        var client = factory.CreateClientFor("admin1", organisationId: null, "Admin");
        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stage Admin OrgA");
        html.Should().Contain("Stage Admin OrgB");
    }

    // ---- POST /Home/SetLanguage ([AllowAnonymous]) --------------------------------------

    [Fact]
    public async Task SetLanguage_WithLocalReturnUrl_SetsCultureCookieAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        // Anonymous client: SetLanguage allows anonymous access.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "nl",
            ["returnUrl"] = "/Activities"
        });

        var response = await client.PostAsync("/Home/SetLanguage", content);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect); // LocalRedirect => 302
        response.Headers.Location!.ToString().Should().Be("/Activities");

        var setCookie = string.Join(";", response.Headers.GetValues("Set-Cookie"));
        setCookie.Should().Contain(CookieRequestCultureProvider.DefaultCookieName);
        setCookie.Should().Contain("nl");
    }

    [Fact]
    public async Task SetLanguage_WithRootReturnUrl_RedirectsToRoot()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "fr",
            ["returnUrl"] = "/"
        });

        var response = await client.PostAsync("/Home/SetLanguage", content);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/");
    }

    [Fact]
    public async Task SetLanguage_MissingReturnUrl_ReturnsBadRequest()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // returnUrl is a non-nullable reference type parameter => implicitly required.
        // When omitted, ModelState is invalid and the action returns BadRequest(ModelState).
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "fr"
        });

        var response = await client.PostAsync("/Home/SetLanguage", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetLanguage_GetVerb_IsNotAllowed()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // SetLanguage is [HttpPost] only; GET should not match.
        var response = await client.GetAsync("/Home/SetLanguage?culture=nl&returnUrl=/");

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    // ---- GET /Home/Error ([AllowAnonymous]) ---------------------------------------------

    [Fact]
    public async Task Error_Anonymous_RendersErrorView()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Home/Error");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Oops!");
    }
}
