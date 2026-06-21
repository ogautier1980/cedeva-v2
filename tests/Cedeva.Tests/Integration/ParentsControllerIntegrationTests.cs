using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class ParentsControllerIntegrationTests
{
    // Valid Belgian national register numbers (pass the modulo-97 checksum).
    private const string ValidParentNrnFormatted = "85.06.15-133.80";
    private const string ValidParentNrnStripped = "85061513380";

    private static Dictionary<string, string> ValidParentForm() => new()
    {
        ["FirstName"] = "Jean",
        ["LastName"] = "Dupont",
        ["Email"] = "jean.dupont@test.be",
        ["MobilePhoneNumber"] = "0470123456",
        ["NationalRegisterNumber"] = ValidParentNrnFormatted,
        ["Street"] = "Rue du Test 1",
        ["City"] = "Bruxelles",
        ["PostalCode"] = "1000",
        ["Country"] = "Belgium"
    };

    // ----------------------------------------------------------------------------
    // GET Index
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task Index_AuthenticatedCoordinator_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            parent.FirstName = "Mireille";
            parent.LastName = "Indexerton";
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Parents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Indexerton");
    }

    [Fact]
    public async Task Index_OnlyShowsParentsOfOwnOrganisation()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation orgA = null!;
        factory.Seed(ctx =>
        {
            orgA = TestData.Organisation("Org A");
            var orgB = TestData.Organisation("Org B");

            var parentA = TestData.Parent(orgA);
            parentA.LastName = "AlphaParent";

            var parentB = TestData.Parent(orgB);
            parentB.LastName = "BravoParent";
            parentB.Email = "bravo@test.be";

            ctx.AddRange(orgA, orgB, parentA, parentB);
            return 0;
        });

        // Coordinator of org A must not see org B parents.
        var client = factory.CreateClientFor("u1", orgA.Id, "Coordinator");
        var response = await client.GetAsync("/Parents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("AlphaParent");
        html.Should().NotContain("BravoParent");
    }

    // ----------------------------------------------------------------------------
    // GET Details
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task Details_ExistingParentInOwnOrganisation_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            parent.LastName = "DetailsParent";
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Parents/Details/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("DetailsParent");
    }

    [Fact]
    public async Task Details_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", 1, "Coordinator");
        var response = await client.GetAsync("/Parents/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_ParentInAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });

        // Coordinator of a different organisation must not access the parent.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/Parents/Details/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------------------------
    // GET Create
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task CreateForm_AuthenticatedCoordinator_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", 1, "Coordinator");
        var response = await client.GetAsync("/Parents/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ----------------------------------------------------------------------------
    // POST Create
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task CreatePost_ValidModel_RedirectsAndPersists()
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
        var response = await client.PostAsync("/Parents/Create", new FormUrlEncodedContent(ValidParentForm()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var persisted = await db.Parents
            .IgnoreQueryFilters()
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.LastName == "Dupont");

        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be("Jean");
        persisted.Email.Should().Be("jean.dupont@test.be");
        // Controller strips NRN formatting before saving.
        persisted.NationalRegisterNumber.Should().Be(ValidParentNrnStripped);
        persisted.OrganisationId.Should().Be(org.Id);
        persisted.Address.Should().NotBeNull();
        persisted.Address.PostalCode.Should().Be("1000");
    }

    [Fact]
    public async Task CreatePost_MissingRequiredFields_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var form = ValidParentForm();
        form["FirstName"] = "";   // required
        form["LastName"] = "";    // required
        form["Email"] = "";       // required

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync("/Parents/Create", new FormUrlEncodedContent(form));

        // Invalid POST re-renders the view (200) rather than redirecting.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        (await db.Parents.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreatePost_InvalidNationalRegisterNumber_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var form = ValidParentForm();
        form["NationalRegisterNumber"] = "85061513381"; // wrong checksum

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync("/Parents/Create", new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        (await db.Parents.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // ----------------------------------------------------------------------------
    // POST Edit
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task EditPost_ValidModel_RedirectsAndUpdates()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            parent.NationalRegisterNumber = ValidParentNrnStripped;
            ctx.AddRange(org, parent);
            return 0;
        });

        var form = ValidParentForm();
        form["Id"] = parent.Id.ToString();
        form["FirstName"] = "Modifié";
        form["LastName"] = "NomModifié";

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync($"/Parents/Edit/{parent.Id}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var updated = await db.Parents.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == parent.Id);
        updated.Should().NotBeNull();
        updated!.FirstName.Should().Be("Modifié");
        updated.LastName.Should().Be("NomModifié");
    }

    [Fact]
    public async Task EditPost_MismatchedRouteAndModelId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });

        var form = ValidParentForm();
        form["Id"] = (parent.Id + 1).ToString(); // mismatch with route id

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync($"/Parents/Edit/{parent.Id}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditPost_InvalidModel_ReturnsViewAndDoesNotUpdate()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            parent.FirstName = "Original";
            ctx.AddRange(org, parent);
            return 0;
        });

        var form = ValidParentForm();
        form["Id"] = parent.Id.ToString();
        form["FirstName"] = ""; // required -> invalid

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync($"/Parents/Edit/{parent.Id}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var unchanged = await db.Parents.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == parent.Id);
        unchanged!.FirstName.Should().Be("Original");
    }

    // ----------------------------------------------------------------------------
    // POST Delete
    // ----------------------------------------------------------------------------

    // DeleteConfirmed for a childless parent removes the parent first, then its Address, in two
    // SaveChanges (the Parent -> Address FK is required and not cascade-configured, so removing the
    // Address before the parent would sever it and throw). Both rows must be gone afterwards.
    [Fact]
    public async Task DeletePost_ChildlessParent_RemovesParentAndAddress()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });
        var addressId = parent.AddressId;

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync($"/Parents/Delete/{parent.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        (await db.Parents.IgnoreQueryFilters().AnyAsync(p => p.Id == parent.Id)).Should().BeFalse();
        (await db.Addresses.IgnoreQueryFilters().AnyAsync(a => a.Id == addressId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletePost_ParentWithChildren_IsNotRemoved()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync($"/Parents/Delete/{parent.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        // Controller redirects with an error message instead of deleting.
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        (await db.Parents.IgnoreQueryFilters().AnyAsync(p => p.Id == parent.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task DeletePost_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", 1, "Coordinator");
        var response = await client.PostAsync("/Parents/Delete/999999",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------------------------
    // Authentication
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Parents");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
