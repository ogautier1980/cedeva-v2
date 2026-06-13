using System.Net;
using System.Text.Json;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Additional ChildrenController integration coverage that complements
/// <see cref="ChildrenControllerIntegrationTests"/> without duplicating its cases.
/// Focus: Index query/redirect/sort/empty branches, Export/ExportPdf, Create GET pre-fill,
/// CreateAjax JSON endpoint, Edit GET + returnUrl flow, Edit POST not-found-after-load,
/// Delete GET render, Details bookings, and extra unauthorized/tenant-isolation cases.
/// </summary>
[Collection("WebApp")]
public class ChildrenControllerCoverageTests
{
    private const string ValidChildNrnFormatted = "16.07.08-164.10";
    private const string ValidChildNrnDigits = "16070816410";

    // ---------------------------------------------------------------------
    // Index: query-string handling (store-to-session + redirect), sort, empty
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithQueryString_RedirectsToCanonicalIndex()
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

        // Any query param triggers the StoreFiltersToSession + RedirectToAction(Index) branch.
        var response = await client.GetAsync("/Children?searchString=foo");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        // RedirectToAction(nameof(Index)) yields "/Children" (no "Index" segment).
        response.Headers.Location!.ToString().Should().Be("/Children");
    }

    [Fact]
    public async Task Index_SearchFilter_NarrowsResults()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var match = TestData.Child(parent);
            match.FirstName = "Uniquematchname";
            match.LastName = "Filtered";
            var other = TestData.Child(parent);
            other.FirstName = "Otherperson";
            other.LastName = "Excluded";
            other.NationalRegisterNumber = "16052012399";
            ctx.AddRange(org, parent, match, other);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        // First request stores the filter in session and redirects.
        var redirect = await client.GetAsync("/Children?searchString=Uniquematchname");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        // Follow-up request (no query) reads the stored filter from session.
        var response = await client.GetAsync("/Children");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Uniquematchname");
        html.Should().NotContain("Otherperson");
    }

    [Fact]
    public async Task Index_ParentFilter_OnlyShowsThatParentsChildren()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parentA = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parentA = TestData.Parent(org);
            parentA.LastName = "ParentAaa";
            var parentB = TestData.Parent(org);
            parentB.LastName = "ParentBbb";
            parentB.Email = "b.parent@test.be";
            parentB.NationalRegisterNumber = "85010112399";

            var childOfA = TestData.Child(parentA);
            childOfA.FirstName = "ChildOfAaa";
            var childOfB = TestData.Child(parentB);
            childOfB.FirstName = "ChildOfBbb";
            childOfB.NationalRegisterNumber = "16052012377";

            ctx.AddRange(org, parentA, parentB, childOfA, childOfB);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var redirect = await client.GetAsync($"/Children?parentId={parentA.Id}");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var response = await client.GetAsync("/Children");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("ChildOfAaa");
        html.Should().NotContain("ChildOfBbb");
    }

    [Theory]
    [InlineData("firstname", "asc")]
    [InlineData("firstname", "desc")]
    [InlineData("lastname", "desc")]
    [InlineData("birthdate", "asc")]
    [InlineData("birthdate", "desc")]
    [InlineData("nationalregisternumber", "asc")]
    [InlineData("nationalregisternumber", "desc")]
    public async Task Index_WithSort_ReturnsOk(string sortBy, string sortOrder)
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var a = TestData.Child(parent);
            a.FirstName = "Anna";
            a.LastName = "Zorro";
            var b = TestData.Child(parent);
            b.FirstName = "Zachary";
            b.LastName = "Adams";
            b.NationalRegisterNumber = "16052012366";
            b.BirthDate = new DateTime(2015, 1, 1);
            ctx.AddRange(org, parent, a, b);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        // Apply sort via query (stores + redirects), then read.
        var redirect = await client.GetAsync($"/Children?sortBy={sortBy}&sortOrder={sortOrder}");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var response = await client.GetAsync("/Children");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Anna");
        html.Should().Contain("Zachary");
    }

    [Fact]
    public async Task Index_NoChildren_RendersEmptyState()
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
        var response = await client.GetAsync("/Children");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // Empty state offers a create call-to-action.
        html.Should().Contain("/Children/Create");
    }

    // ---------------------------------------------------------------------
    // Export (Excel) and ExportPdf
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Export_ReturnsExcelFileWithContent()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Children/Export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_WithSearchFilter_ReturnsExcelFile()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            child.LastName = "Exportfiltered";
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Children/Export?searchString=Exportfiltered");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportPdf_ReturnsPdfFileWithContent()
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
        var response = await client.GetAsync($"/Children/ExportPdf?parentId={parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Children/Export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // GET Create with parentId pre-selection
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_Get_WithParentId_RendersForm()
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
        var response = await client.GetAsync($"/Children/Create?parentId={parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // CreateAjax (JSON endpoint)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreateAjax_Valid_ReturnsSuccessJsonAndPersists()
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
            ["FirstName"] = "Ajax",
            ["LastName"] = "Created",
            ["NationalRegisterNumber"] = ValidChildNrnFormatted,
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync("/Children/CreateAjax", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("childId").GetInt32().Should().BeGreaterThan(0);
        doc.RootElement.GetProperty("childName").GetString().Should().Contain("Ajax");

        using var db = factory.NewDbContext();
        var persisted = await db.Children.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.LastName == "Created" && c.FirstName == "Ajax");
        persisted.Should().NotBeNull();
        // Formatting stripped before persist.
        persisted!.NationalRegisterNumber.Should().Be(ValidChildNrnDigits);
    }

    [Fact]
    public async Task CreateAjax_Invalid_ReturnsErrorJsonAndDoesNotPersist()
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
        // Missing FirstName / LastName / NRN -> invalid model.
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync("/Children/CreateAjax", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();

        using var db = factory.NewDbContext();
        (await db.Children.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // GET Edit
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Edit_Get_ExistingChild_RendersFormWithFormattedNrn()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            child = TestData.Child(parent);
            child.FirstName = "Editablename";
            child.NationalRegisterNumber = ValidChildNrnDigits;
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Children/Edit/{child.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Editablename");
        // NRN is displayed formatted in the edit form.
        html.Should().Contain(ValidChildNrnFormatted);
    }

    [Fact]
    public async Task Edit_Get_UnknownChild_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, "Coordinator");
        var response = await client.GetAsync("/Children/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_Get_ChildInAnotherOrganisation_ReturnsNotFound()
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

        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/Children/Edit/{child.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // POST Edit: returnUrl redirect + not-found-after-load
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Edit_Post_Valid_WithReturnUrl_RedirectsToReturnUrl()
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
            ["FirstName"] = "Returned",
            ["LastName"] = "Child",
            ["NationalRegisterNumber"] = ValidChildNrnFormatted,
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync(
            $"/Children/Edit/{child.Id}?returnUrl=%2FChildren%3FsearchString%3Dx", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Children");

        using var db = factory.NewDbContext();
        var updated = await db.Children.IgnoreQueryFilters().FirstAsync(c => c.Id == child.Id);
        updated.FirstName.Should().Be("Returned");
    }

    [Fact]
    public async Task Edit_Post_ValidModelButChildMissing_ReturnsNotFound()
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
        const int missingId = 424242;
        // Valid model, route id == body id, but no such child exists -> NotFound after load.
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = missingId.ToString(),
            ["FirstName"] = "Ghost",
            ["LastName"] = "Child",
            ["NationalRegisterNumber"] = ValidChildNrnFormatted,
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = parent.Id.ToString()
        });

        var response = await client.PostAsync($"/Children/Edit/{missingId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // GET Delete (render) + Details with bookings
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Delete_Get_ExistingChild_RendersConfirmation()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            child = TestData.Child(parent);
            child.FirstName = "Deletablename";
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Children/Delete/{child.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Deletablename");
    }

    [Fact]
    public async Task Details_ChildWithBooking_ShowsActivityInfo()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            child = TestData.Child(parent);
            child.FirstName = "Bookedchild";
            var activity = TestData.Activity(org, "Stage Details Coverage");
            var booking = TestData.Booking(child, activity, null, totalAmount: 100m, paidAmount: 0m);
            ctx.AddRange(org, parent, child, activity, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Children/Details/{child.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Bookedchild");
        html.Should().Contain("Stage Details Coverage");
    }

    // ---------------------------------------------------------------------
    // Extra unauthorized coverage for write endpoints
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_Post_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "No",
            ["LastName"] = "Auth",
            ["NationalRegisterNumber"] = ValidChildNrnFormatted,
            ["BirthDate"] = "2016-07-08",
            ["ParentId"] = "1"
        });

        var response = await client.PostAsync("/Children/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var db = factory.NewDbContext();
        (await db.Children.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Post_ChildInAnotherOrganisation_ReturnsNotFoundAndKeepsChild()
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

        // Coordinator of another org: tenancy filter hides the child -> NotFound.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = child.Id.ToString()
        });

        var response = await client.PostAsync($"/Children/Delete/{child.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var db = factory.NewDbContext();
        (await db.Children.IgnoreQueryFilters().AnyAsync(c => c.Id == child.Id)).Should().BeTrue();
    }
}
