using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Additional integration coverage for <c>OrganisationsController</c> covering branches the
/// baseline <see cref="OrganisationsControllerIntegrationTests"/> does not exercise:
/// Index sorting/search/filter (with session persistence across the clean-URL redirect) and
/// summary counts, Edit GET/POST returnUrl handling, the DeleteLogo AJAX endpoint, the Excel
/// and PDF export endpoints, and the authorization surface of those extra endpoints.
/// The controller is <c>[Authorize(Roles = "Admin")]</c>, so Coordinators are Forbidden and
/// unauthenticated requests are challenged everywhere.
/// </summary>
[Collection("WebApp")]
public class OrganisationsControllerCoverageTests
{
    private static HttpClient AdminClient(CedevaWebApplicationFactory factory) =>
        factory.CreateClientFor("admin-user", organisationId: null, role: "Admin");

    // ---------------------------------------------------------------------
    // GET Index — sorting branches
    // ---------------------------------------------------------------------

    // The Index action stores incoming query params in the session, sets a "keep filters" flag,
    // and redirects to the clean URL. Following that redirect with the SAME client (the session
    // cookie is carried) reloads the persisted params and renders the filtered/sorted list.
    private static async Task<string> IndexWithParamsAsync(HttpClient client, string query)
    {
        var redirect = await client.GetAsync($"/Organisations?{query}");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var location = redirect.Headers.Location!.ToString();
        var followed = await client.GetAsync(location);
        followed.StatusCode.Should().Be(HttpStatusCode.OK);
        return await followed.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Index_SortByNameDescending_OrdersZBeforeA()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Alpha Org"));
            ctx.Add(TestData.Organisation("Zeta Org"));
            return 0;
        });

        var html = await IndexWithParamsAsync(AdminClient(factory), "SortBy=Name&SortOrder=desc");

        html.Should().Contain("Alpha Org").And.Contain("Zeta Org");
        // Descending => Zeta appears before Alpha in the rendered table.
        html.IndexOf("Zeta Org", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Alpha Org", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Index_SortByCityAscending_OrdersByCity()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var amsterdam = TestData.Organisation("Org In Amsterdam");
            amsterdam.Address!.City = "Amsterdam";
            var zurich = TestData.Organisation("Org In Zurich");
            zurich.Address!.City = "Zurich";
            ctx.Add(zurich);
            ctx.Add(amsterdam);
            return 0;
        });

        var html = await IndexWithParamsAsync(AdminClient(factory), "SortBy=City&SortOrder=asc");

        html.IndexOf("Org In Amsterdam", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Org In Zurich", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Index_SortByCityDescending_OrdersByCity()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var amsterdam = TestData.Organisation("Org In Amsterdam");
            amsterdam.Address!.City = "Amsterdam";
            var zurich = TestData.Organisation("Org In Zurich");
            zurich.Address!.City = "Zurich";
            ctx.Add(amsterdam);
            ctx.Add(zurich);
            return 0;
        });

        var html = await IndexWithParamsAsync(AdminClient(factory), "SortBy=City&SortOrder=desc");

        html.IndexOf("Org In Zurich", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Org In Amsterdam", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Index_SortByPostalCodeAscending_OrdersByPostalCode()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var low = TestData.Organisation("Org Low Postal");
            low.Address!.PostalCode = "1000";
            var high = TestData.Organisation("Org High Postal");
            high.Address!.PostalCode = "9000";
            ctx.Add(high);
            ctx.Add(low);
            return 0;
        });

        var html = await IndexWithParamsAsync(AdminClient(factory), "SortBy=PostalCode&SortOrder=asc");

        html.IndexOf("Org Low Postal", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Org High Postal", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Index_SortByPostalCodeDescending_OrdersByPostalCode()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var low = TestData.Organisation("Org Low Postal");
            low.Address!.PostalCode = "1000";
            var high = TestData.Organisation("Org High Postal");
            high.Address!.PostalCode = "9000";
            ctx.Add(low);
            ctx.Add(high);
            return 0;
        });

        var html = await IndexWithParamsAsync(AdminClient(factory), "SortBy=PostalCode&SortOrder=desc");

        html.IndexOf("Org High Postal", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Org Low Postal", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Index_UnknownSortBy_FallsBackToNameAscending()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Beta Org"));
            ctx.Add(TestData.Organisation("Alpha Org"));
            return 0;
        });

        // An unrecognised SortBy hits the default arm => order by Name ascending.
        var html = await IndexWithParamsAsync(AdminClient(factory), "SortBy=Bogus&SortOrder=asc");

        html.IndexOf("Alpha Org", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Beta Org", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // GET Index — search filter persisted through the redirect
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithSearchString_FiltersResultsAfterRedirect()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Findable Org"));
            ctx.Add(TestData.Organisation("Hidden Group"));
            return 0;
        });

        var html = await IndexWithParamsAsync(AdminClient(factory), "SearchString=Findable");

        html.Should().Contain("Findable Org");
        html.Should().NotContain("Hidden Group");
    }

    [Fact]
    public async Task Index_NavigationWithoutKeepFlag_ClearsPreviousFilters()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Findable Org"));
            ctx.Add(TestData.Organisation("Hidden Group"));
            return 0;
        });

        var client = AdminClient(factory);

        // First, apply and follow a search (stores in session).
        await IndexWithParamsAsync(client, "SearchString=Findable");

        // A plain navigation (no query params, no keep-filters TempData) clears the session filters,
        // so the previously hidden organisation reappears.
        var plain = await client.GetAsync("/Organisations");
        plain.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await plain.Content.ReadAsStringAsync();
        html.Should().Contain("Findable Org").And.Contain("Hidden Group");
    }

    // ---------------------------------------------------------------------
    // GET Index — summary counts (Activities / Parents / TeamMembers)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_RendersWithRelatedEntityGraph()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation("Org With Children");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var activity = TestData.Activity(org);
            ctx.Add(org);
            ctx.Add(parent);
            ctx.Add(child);
            ctx.Add(activity);
            return org;
        });

        var response = await AdminClient(factory).GetAsync("/Organisations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Org With Children");
    }

    // ---------------------------------------------------------------------
    // GET / POST Edit — returnUrl handling
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Edit_Get_WithReturnUrl_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("Org With Return");
            ctx.Add(o);
            return o;
        });

        var response = await AdminClient(factory)
            .GetAsync($"/Organisations/Edit/{org.Id}?returnUrl=%2FOrganisations%2FDetails%2F{org.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Post_Valid_WithReturnUrl_RedirectsToReturnUrl()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("Org Pre Edit");
            ctx.Add(o);
            return o;
        });

        var returnUrl = "/Organisations";
        var fields = new Dictionary<string, string>
        {
            ["Id"] = org.Id.ToString(),
            ["Name"] = "Org Post Edit",
            ["Description"] = "Une description suffisamment longue pour valider",
            ["Street"] = "Rue de la Loi",
            ["City"] = "Bruxelles",
            ["PostalCode"] = "1000",
            ["Country"] = "Belgium"
        };

        var response = await AdminClient(factory).PostAsync(
            $"/Organisations/Edit/{org.Id}?returnUrl={Uri.EscapeDataString(returnUrl)}",
            new FormUrlEncodedContent(fields));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be(returnUrl);

        using var db = factory.NewDbContext();
        db.Organisations.Single(o => o.Id == org.Id).Name.Should().Be("Org Post Edit");
    }

    // ---------------------------------------------------------------------
    // POST DeleteLogo
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeleteLogo_NoLogo_ReturnsOkAndLeavesOrganisation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("Org No Logo");
            ctx.Add(o);
            return o;
        });

        var response = await AdminClient(factory).PostAsync(
            $"/Organisations/DeleteLogo/{org.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Organisations.Single(o => o.Id == org.Id).LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLogo_WithLogo_ClearsLogoUrlAndReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("Org With Logo");
            o.LogoUrl = "1/logos/some-existing-logo.png";
            ctx.Add(o);
            return o;
        });

        var response = await AdminClient(factory).PostAsync(
            $"/Organisations/DeleteLogo/{org.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        // The storage delete is wrapped in try/catch, so a missing physical file does not fail it.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Organisations.Single(o => o.Id == org.Id).LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLogo_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var response = await AdminClient(factory).PostAsync(
            "/Organisations/DeleteLogo/999999",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteLogo_AsCoordinator_IsForbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation();
            ctx.Add(o);
            return o;
        });

        var client = factory.CreateClientFor("coord", organisationId: org.Id, role: "Coordinator");
        var response = await client.PostAsync(
            $"/Organisations/DeleteLogo/{org.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------
    // GET Export (Excel)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Export_AsAdmin_ReturnsNonEmptyExcel()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Exportable Org"));
            return 0;
        });

        var response = await AdminClient(factory).GetAsync("/Organisations/Export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_WithSearchString_ReturnsNonEmptyExcel()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Exportable Org"));
            ctx.Add(TestData.Organisation("Other Org"));
            return 0;
        });

        var response = await AdminClient(factory).GetAsync("/Organisations/Export?searchString=Exportable");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_AsCoordinator_IsForbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("coord", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/Organisations/Export");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------
    // GET ExportPdf
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExportPdf_AsAdmin_ReturnsNonEmptyPdf()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Pdf Org"));
            return 0;
        });

        var response = await AdminClient(factory).GetAsync("/Organisations/ExportPdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportPdf_WithSearchString_ReturnsNonEmptyPdf()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Pdf Org"));
            ctx.Add(TestData.Organisation("Excluded Org"));
            return 0;
        });

        var response = await AdminClient(factory).GetAsync("/Organisations/ExportPdf?searchString=Pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportPdf_AsCoordinator_IsForbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("coord", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/Organisations/ExportPdf");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Export_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Organisations/Export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
