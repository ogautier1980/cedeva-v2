using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class ActivitiesControllerIntegrationTests
{
    private static FormUrlEncodedContent ValidActivityForm(
        string name = "Stage Nouveau",
        string description = "Description du stage",
        string startDate = "2026-07-01",
        string endDate = "2026-07-05",
        string isActive = "true",
        string pricePerDay = "20",
        int organisationId = 0,
        int id = 0)
    {
        var fields = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Description"] = description,
            ["StartDate"] = startDate,
            ["EndDate"] = endDate,
            ["IsActive"] = isActive,
            ["PricePerDay"] = pricePerDay,
            ["OrganisationId"] = organisationId.ToString(),
            ["Id"] = id.ToString()
        };
        return new FormUrlEncodedContent(fields);
    }

    // ---------- Authentication ----------

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Activities");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePost_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/Activities/Create", ValidActivityForm());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- GET Index ----------

    [Fact]
    public async Task Index_AuthenticatedCoordinator_ReturnsOkAndListsOwnActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var a = TestData.Activity(org, "StageListe");
            ctx.AddRange(org, a);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("StageListe");
    }

    [Fact]
    public async Task Index_Coordinator_DoesNotSeeOtherOrganisationsActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var orgA = TestData.Organisation("Org A");
            var a = TestData.Activity(orgA, "StageSecretOrgA");
            ctx.AddRange(orgA, a);
            return 0;
        });

        // Coordinator of a different organisation should not see org A's activity in the list.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync("/Activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("StageSecretOrgA");
    }

    // ---------- GET Details ----------

    [Fact]
    public async Task Details_ExistingActivityInOwnOrganisation_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageDetail");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Activities/Details/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("StageDetail");
    }

    [Fact]
    public async Task Details_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, "Coordinator");
        var response = await client.GetAsync("/Activities/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_ActivityInAnotherOrganisation_ReturnsNotFound()
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

        // Coordinator of a different organisation: query filter hides the activity -> NotFound.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/Activities/Details/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- GET Create ----------

    [Fact]
    public async Task CreateForm_Coordinator_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, "Coordinator");
        var response = await client.GetAsync("/Activities/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- POST Create ----------

    [Fact]
    public async Task CreatePost_ValidCoordinator_RedirectsAndPersistsInOwnOrganisation()
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
        var response = await client.PostAsync("/Activities/Create",
            ValidActivityForm(name: "StageCree", organisationId: 0));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");

        using var db = factory.NewDbContext();
        var created = await db.Activities.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Name == "StageCree");
        created.Should().NotBeNull();
        // OrganisationId is forced to the coordinator's own organisation, not the posted value.
        created!.OrganisationId.Should().Be(org.Id);
    }

    [Fact]
    public async Task CreatePost_GeneratesActivityDaysForDateRange()
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
        var response = await client.PostAsync("/Activities/Create",
            ValidActivityForm(name: "StageJours", startDate: "2026-07-01", endDate: "2026-07-05"));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var created = await db.Activities.IgnoreQueryFilters()
            .Include(a => a.Days)
            .FirstOrDefaultAsync(a => a.Name == "StageJours");
        created.Should().NotBeNull();
        // 2026-07-01 to 2026-07-05 inclusive => 5 days generated.
        created!.Days.Should().HaveCount(5);
    }

    [Fact]
    public async Task CreatePost_MissingRequiredName_ReturnsOkAndDoesNotPersist()
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
        var response = await client.PostAsync("/Activities/Create",
            ValidActivityForm(name: "", description: "SansNom"));

        // Invalid POST re-renders the view with validation errors (200), no redirect.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var any = await db.Activities.IgnoreQueryFilters()
            .AnyAsync(a => a.Description == "SansNom");
        any.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePost_EndDateBeforeStartDate_ReturnsOkAndDoesNotPersist()
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
        var response = await client.PostAsync("/Activities/Create",
            ValidActivityForm(name: "StageDatesInversees", startDate: "2026-07-10", endDate: "2026-07-01"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var any = await db.Activities.IgnoreQueryFilters()
            .AnyAsync(a => a.Name == "StageDatesInversees");
        any.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePost_AdminWithoutOrganisation_ReturnsOkAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        // Admin must select an organisation; OrganisationId == 0 triggers a model error.
        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.PostAsync("/Activities/Create",
            ValidActivityForm(name: "StageAdminSansOrg", organisationId: 0));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var any = await db.Activities.IgnoreQueryFilters()
            .AnyAsync(a => a.Name == "StageAdminSansOrg");
        any.Should().BeFalse();
    }

    // ---------- POST Edit ----------

    [Fact]
    public async Task EditPost_ValidChange_RedirectsAndPersists()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "NomOriginal");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = ValidActivityForm(
            name: "NomModifie",
            startDate: "2026-07-01",
            endDate: "2026-07-05",
            organisationId: org.Id,
            id: activity.Id);

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var updated = await db.Activities.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == activity.Id);
        updated!.Name.Should().Be("NomModifie");
    }

    [Fact]
    public async Task EditPost_UnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, "Coordinator");
        // Route id and bound viewModel.Id match (both 999999) but no such activity exists.
        var form = ValidActivityForm(organisationId: 1, id: 999999);
        var response = await client.PostAsync("/Activities/Edit/999999", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditPost_InvalidModel_ReturnsOkAndDoesNotPersistChange()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Inchange");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = ValidActivityForm(name: "", organisationId: org.Id, id: activity.Id);
        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var unchanged = await db.Activities.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == activity.Id);
        unchanged!.Name.Should().Be("Inchange");
    }

    // ---------- POST Delete ----------

    [Fact]
    public async Task DeletePost_ActivityWithoutBookings_RedirectsAndRemoves()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "ASupprimer");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = activity.Id.ToString() });
        var response = await client.PostAsync($"/Activities/Delete/{activity.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var gone = await db.Activities.IgnoreQueryFilters()
            .AnyAsync(a => a.Id == activity.Id);
        gone.Should().BeFalse();
    }

    [Fact]
    public async Task DeletePost_ActivityWithBookings_RedirectsAndDoesNotRemove()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "AvecReservations");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, totalAmount: 100m, paidAmount: 0m);
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = activity.Id.ToString() });
        var response = await client.PostAsync($"/Activities/Delete/{activity.Id}", form);

        // Controller blocks deletion of activities with bookings and redirects with an error message.
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var stillThere = await db.Activities.IgnoreQueryFilters()
            .AnyAsync(a => a.Id == activity.Id);
        stillThere.Should().BeTrue();
    }

    [Fact]
    public async Task DeletePost_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "999999" });
        var response = await client.PostAsync("/Activities/Delete/999999", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
