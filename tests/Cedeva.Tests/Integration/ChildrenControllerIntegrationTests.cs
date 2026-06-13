using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class ChildrenControllerIntegrationTests
{
    // Valid Belgian national register numbers (pass the modulo-97 check used by the validator).
    private const string ValidChildNrnFormatted = "16.07.08-164.10";
    private const string ValidChildNrnDigits = "16070816410";

    // ---------------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Children");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Details_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Children/Details/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // GET Index
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_AuthenticatedUser_ListsOwnOrganisationChildren()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            child.FirstName = "Chloetelle";
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Children");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Chloetelle");
    }

    [Fact]
    public async Task Index_DoesNotListAnotherOrganisationChildren()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            child.FirstName = "ZZSecretName";
            ctx.AddRange(org, parent, child);
            return 0;
        });

        // Coordinator of a different organisation must not see org A's children.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync("/Children");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("ZZSecretName");
    }

    // ---------------------------------------------------------------------
    // GET Details
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Details_ExistingChildInOwnOrganisation_RendersDetails()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            child = TestData.Child(parent);
            child.FirstName = "Detailsname";
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Children/Details/{child.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Detailsname");
    }

    [Fact]
    public async Task Details_UnknownChild_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, "Coordinator");
        var response = await client.GetAsync("/Children/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_ChildInAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Child child = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var parent = TestData.Parent(org);
            child = TestData.Child(parent);
            ctx.AddRange(org, parent, child);
            return 0;
        });

        // Coordinator of org B must not see org A's child.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/Children/Details/{child.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // GET Create form
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_Get_RendersForm()
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
        var response = await client.GetAsync("/Children/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // POST Create
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_Post_Valid_PersistsChildAndRedirects()
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

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Nouveau",
            ["LastName"] = "Created",
            ["NationalRegisterNumber"] = ValidChildNrnFormatted,
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync("/Children/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var persisted = await db.Children.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.LastName == "Created");
        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be("Nouveau");
        // Formatting is stripped before persisting.
        persisted.NationalRegisterNumber.Should().Be(ValidChildNrnDigits);
        persisted.ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Create_Post_MissingRequiredFields_ReturnsViewAndDoesNotPersist()
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

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        // Missing FirstName / LastName / NationalRegisterNumber.
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync("/Children/Create", form);

        // Invalid model re-renders the view (200), no redirect.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        (await db.Children.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Create_Post_InvalidNationalRegisterNumber_ReturnsViewAndDoesNotPersist()
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

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Bad",
            ["LastName"] = "Nrn",
            // 11 digits (passes StringLength) but fails the modulo-97 check.
            ["NationalRegisterNumber"] = "16.07.08-164.11",
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync("/Children/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        (await db.Children.IgnoreQueryFilters().AnyAsync(c => c.LastName == "Nrn")).Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // POST Edit
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Edit_Post_Valid_UpdatesChildAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            child = TestData.Child(parent);
            child.NationalRegisterNumber = ValidChildNrnDigits;
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = child.Id.ToString(),
            ["FirstName"] = "Renamed",
            ["LastName"] = "Child",
            ["NationalRegisterNumber"] = ValidChildNrnFormatted,
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync($"/Children/Edit/{child.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var updated = await db.Children.IgnoreQueryFilters().FirstAsync(c => c.Id == child.Id);
        updated.FirstName.Should().Be("Renamed");
    }

    [Fact]
    public async Task Edit_Post_MismatchedRouteAndBodyId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            child = TestData.Child(parent);
            child.NationalRegisterNumber = ValidChildNrnDigits;
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = (child.Id + 1000).ToString(), // body id differs from route id
            ["FirstName"] = "Renamed",
            ["LastName"] = "Child",
            ["NationalRegisterNumber"] = ValidChildNrnFormatted,
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync($"/Children/Edit/{child.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_Post_Invalid_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            child = TestData.Child(parent);
            child.FirstName = "Original";
            child.NationalRegisterNumber = ValidChildNrnDigits;
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = child.Id.ToString(),
            ["FirstName"] = "", // required -> invalid
            ["LastName"] = "Child",
            ["NationalRegisterNumber"] = ValidChildNrnFormatted,
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync($"/Children/Edit/{child.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var unchanged = await db.Children.IgnoreQueryFilters().FirstAsync(c => c.Id == child.Id);
        unchanged.FirstName.Should().Be("Original");
    }

    // ---------------------------------------------------------------------
    // GET / POST Delete
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Delete_Get_UnknownChild_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, "Coordinator");
        var response = await client.GetAsync("/Children/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Post_RemovesChildAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            child = TestData.Child(parent);
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = child.Id.ToString()
        });

        var response = await client.PostAsync($"/Children/Delete/{child.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        (await db.Children.IgnoreQueryFilters().AnyAsync(c => c.Id == child.Id)).Should().BeFalse();
    }
}
