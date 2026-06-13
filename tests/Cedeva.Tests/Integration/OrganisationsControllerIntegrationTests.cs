using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration tests for <c>OrganisationsController</c>. The controller is decorated with
/// <c>[Authorize(Roles = "Admin")]</c>, so every endpoint requires an authenticated Admin.
/// A Coordinator is therefore Forbidden everywhere, and an unauthenticated request is challenged.
/// </summary>
[Collection("WebApp")]
public class OrganisationsControllerIntegrationTests
{
    private static HttpClient AdminClient(CedevaWebApplicationFactory factory) =>
        factory.CreateClientFor("admin-user", organisationId: null, role: "Admin");

    private static FormUrlEncodedContent ValidForm(
        string? id = null,
        string name = "Nouvelle Organisation",
        string description = "Description suffisamment longue",
        string street = "Rue de la Loi",
        string city = "Bruxelles",
        string postalCode = "1000")
    {
        var fields = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Description"] = description,
            ["Street"] = street,
            ["City"] = city,
            ["PostalCode"] = postalCode,
            ["Country"] = "Belgium"
        };
        if (id != null) fields["Id"] = id;
        return new FormUrlEncodedContent(fields);
    }

    // ---------------------------------------------------------------------
    // Authentication / authorization
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Organisations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Index_AsCoordinator_IsForbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("coord", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/Organisations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Details_AsCoordinator_IsForbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation();
            ctx.Add(o);
            return o;
        });

        var client = factory.CreateClientFor("coord", organisationId: org.Id, role: "Coordinator");
        var response = await client.GetAsync($"/Organisations/Details/{org.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------
    // GET Index
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_AsAdmin_ReturnsOkAndListsOrganisations()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Alpha Org"));
            ctx.Add(TestData.Organisation("Beta Org"));
            return 0;
        });

        var response = await AdminClient(factory).GetAsync("/Organisations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Alpha Org").And.Contain("Beta Org");
    }

    [Fact]
    public async Task Index_WithSearchString_RedirectsToCleanUrl()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        // Providing a query parameter triggers the store-and-redirect-to-clean-URL branch.
        var response = await AdminClient(factory).GetAsync("/Organisations?SearchString=Alpha");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Organisations");
    }

    // ---------------------------------------------------------------------
    // GET Details
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Details_KnownId_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        // Plain ASCII name: it is rendered as text and accents would be HTML-encoded in the markup.
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("Org Detail Page");
            ctx.Add(o);
            return o;
        });

        var response = await AdminClient(factory).GetAsync($"/Organisations/Details/{org.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Org Detail Page");
    }

    [Fact]
    public async Task Details_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var response = await AdminClient(factory).GetAsync("/Organisations/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // GET Create
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_Get_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var response = await AdminClient(factory).GetAsync("/Organisations/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // POST Create
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_Post_Valid_RedirectsToDetailsAndPersists()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var response = await AdminClient(factory).PostAsync(
            "/Organisations/Create",
            ValidForm(name: "Org Créée", description: "Une description bien assez longue"));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Details");

        using var db = factory.NewDbContext();
        var created = db.Organisations.SingleOrDefault(o => o.Name == "Org Créée");
        created.Should().NotBeNull();
        created!.Description.Should().Be("Une description bien assez longue");
    }

    [Fact]
    public async Task Create_Post_MissingName_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var response = await AdminClient(factory).PostAsync(
            "/Organisations/Create",
            ValidForm(name: "")); // Name is required

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-rendered view, not a redirect

        using var db = factory.NewDbContext();
        db.Organisations.Count().Should().Be(0);
    }

    [Fact]
    public async Task Create_Post_DescriptionTooShort_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var response = await AdminClient(factory).PostAsync(
            "/Organisations/Create",
            ValidForm(description: "court")); // < 10 chars violates StringLength MinimumLength

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Organisations.Count().Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // GET Edit
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Edit_Get_KnownId_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        // Plain ASCII name: the Edit form renders it inside an input value attribute (accents encoded).
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("Org To Edit");
            ctx.Add(o);
            return o;
        });

        var response = await AdminClient(factory).GetAsync($"/Organisations/Edit/{org.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Org To Edit");
    }

    [Fact]
    public async Task Edit_Get_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var response = await AdminClient(factory).GetAsync("/Organisations/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // POST Edit
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Edit_Post_Valid_RedirectsAndPersistsChanges()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("Ancien Nom");
            ctx.Add(o);
            return o;
        });

        var response = await AdminClient(factory).PostAsync(
            $"/Organisations/Edit/{org.Id}",
            ValidForm(id: org.Id.ToString(), name: "Nouveau Nom",
                description: "Une description mise à jour et longue", city: "Liège"));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var updated = db.Organisations.Single(o => o.Id == org.Id);
        updated.Name.Should().Be("Nouveau Nom");
        updated.Description.Should().Be("Une description mise à jour et longue");
    }

    [Fact]
    public async Task Edit_Post_IdMismatch_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation();
            ctx.Add(o);
            return o;
        });

        // Route id differs from the body Id => NotFound before any validation.
        var response = await AdminClient(factory).PostAsync(
            $"/Organisations/Edit/{org.Id}",
            ValidForm(id: (org.Id + 1).ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_Post_Invalid_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("Nom Original");
            ctx.Add(o);
            return o;
        });

        var response = await AdminClient(factory).PostAsync(
            $"/Organisations/Edit/{org.Id}",
            ValidForm(id: org.Id.ToString(), name: "")); // invalid: Name required

        response.StatusCode.Should().Be(HttpStatusCode.OK); // view re-rendered

        using var db = factory.NewDbContext();
        db.Organisations.Single(o => o.Id == org.Id).Name.Should().Be("Nom Original");
    }

    // ---------------------------------------------------------------------
    // GET Delete (confirmation page)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Delete_Get_KnownId_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("À Supprimer");
            ctx.Add(o);
            return o;
        });

        var response = await AdminClient(factory).GetAsync($"/Organisations/Delete/{org.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_Get_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var response = await AdminClient(factory).GetAsync("/Organisations/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // POST Delete (DeleteConfirmed)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Delete_Post_RemovesOrganisationAndRedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation("Org Jetable");
            ctx.Add(o);
            return o;
        });

        var response = await AdminClient(factory).PostAsync(
            $"/Organisations/Delete/{org.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Organisations");

        using var db = factory.NewDbContext();
        db.Organisations.Any(o => o.Id == org.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Post_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var response = await AdminClient(factory).PostAsync(
            "/Organisations/Delete/999999",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
