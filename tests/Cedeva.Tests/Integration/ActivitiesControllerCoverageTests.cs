using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Additional coverage for <c>ActivitiesController</c> exercising actions and branches not
/// covered by <see cref="ActivitiesControllerIntegrationTests"/>: GET Edit / GET Delete views,
/// the GET Index search/sort/filter pipeline (store-to-session + clean-URL redirect), the admin
/// Create flow, Excel/PDF export endpoints, and tenant-isolation on the edit/delete/export paths.
/// </summary>
[Collection("WebApp")]
public class ActivitiesControllerCoverageTests
{
    private static FormUrlEncodedContent ActivityForm(
        string name = "Stage Couverture",
        string description = "Description",
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

    // ---------- GET Index: search / sort / filter pipeline ----------

    [Fact]
    public async Task Index_WithQueryParams_RedirectsToCleanUrl()
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
        var response = await client.GetAsync("/Activities?searchString=anything");

        // Any query param triggers store-to-session then a redirect to the clean Index URL.
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");
        response.Headers.Location!.ToString().Should().NotContain("searchString");
    }

    [Fact]
    public async Task Index_SearchTerm_FiltersToMatchingActivitiesOnly()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.AddRange(org,
                TestData.Activity(org, "AlphaUnique"),
                TestData.Activity(org, "BetaUnique"));
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        // First request stores the filter in session and redirects (cookies retained on client).
        var redirect = await client.GetAsync("/Activities?searchString=AlphaUnique");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        // Follow-up clean request reads the filter from session and applies it.
        var listed = await client.GetAsync("/Activities");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await listed.Content.ReadAsStringAsync();
        html.Should().Contain("AlphaUnique");
        html.Should().NotContain("BetaUnique");
    }

    [Fact]
    public async Task Index_ShowActiveOnly_HidesInactiveActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var active = TestData.Activity(org, "ActiveStageX");
            var inactive = TestData.Activity(org, "InactiveStageX");
            inactive.IsActive = false;
            ctx.AddRange(org, active, inactive);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var redirect = await client.GetAsync("/Activities?showActiveOnly=true");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var listed = await client.GetAsync("/Activities");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await listed.Content.ReadAsStringAsync();
        html.Should().Contain("ActiveStageX");
        html.Should().NotContain("InactiveStageX");
    }

    [Fact]
    public async Task Index_SortByNameAscending_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.AddRange(org,
                TestData.Activity(org, "ZuluSort"),
                TestData.Activity(org, "AlphaSort"));
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var redirect = await client.GetAsync("/Activities?sortBy=name&sortOrder=asc");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var listed = await client.GetAsync("/Activities");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await listed.Content.ReadAsStringAsync();
        // Ascending by name => AlphaSort appears before ZuluSort in the rendered table.
        html.IndexOf("AlphaSort", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("ZuluSort", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Index_Admin_SeesAllOrganisationsActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var orgA = TestData.Organisation("Org Alpha");
            var orgB = TestData.Organisation("Org Beta");
            ctx.AddRange(orgA, orgB,
                TestData.Activity(orgA, "StageOrgAlpha"),
                TestData.Activity(orgB, "StageOrgBeta"));
            return 0;
        });

        // Admin bypasses the multi-tenancy filter and sees every organisation's activities.
        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.GetAsync("/Activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("StageOrgAlpha");
        html.Should().Contain("StageOrgBeta");
    }

    // ---------- GET Create (admin loads organisations) ----------

    [Fact]
    public async Task CreateForm_Admin_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("OrgForAdminCreate"));
            return 0;
        });

        // Admin branch loads the organisations dropdown (ViewBag.Organisations).
        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.GetAsync("/Activities/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- POST Create (admin chooses organisation) ----------

    [Fact]
    public async Task CreatePost_AdminWithOrganisation_PersistsToChosenOrganisation()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation("OrgAdminPicks");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.PostAsync("/Activities/Create",
            ActivityForm(name: "StageAdminChoisi", organisationId: org.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var created = await db.Activities.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Name == "StageAdminChoisi");
        created.Should().NotBeNull();
        created!.OrganisationId.Should().Be(org.Id);
    }

    [Fact]
    public async Task CreatePost_SingleDayRange_GeneratesOneDay()
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
            ActivityForm(name: "StageUnJour", startDate: "2026-07-01", endDate: "2026-07-01"));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var created = await db.Activities.IgnoreQueryFilters()
            .Include(a => a.Days)
            .FirstOrDefaultAsync(a => a.Name == "StageUnJour");
        // StartDate == EndDate => exactly one day generated (boundary of the <= loop).
        created!.Days.Should().HaveCount(1);
    }

    // ---------- GET Edit ----------

    [Fact]
    public async Task EditForm_ExistingActivityInOwnOrganisation_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageEditGet");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Activities/Edit/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("StageEditGet");
    }

    [Fact]
    public async Task EditForm_BackfillsMissingDays()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            // 2026-07-01 .. 2026-07-05 inclusive => 5 days, but none seeded.
            activity = TestData.Activity(org, "StageSansJours");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Activities/Edit/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET Edit calls EnsureAllDaysExist + SaveChanges, materialising the 5 missing days.
        using var db = factory.NewDbContext();
        var dayCount = await db.Set<ActivityDay>().IgnoreQueryFilters()
            .CountAsync(d => d.ActivityId == activity.Id);
        dayCount.Should().Be(5);
    }

    [Fact]
    public async Task EditForm_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, "Coordinator");
        var response = await client.GetAsync("/Activities/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditForm_ActivityInAnotherOrganisation_ReturnsNotFound()
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

        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/Activities/Edit/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- POST Edit tenant isolation ----------

    [Fact]
    public async Task EditPost_ActivityInAnotherOrganisation_ReturnsNotFoundAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "NomProtege");
            ctx.AddRange(org, activity);
            return 0;
        });

        // Coordinator of a foreign org: the activity is hidden by the query filter => NotFound.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var form = ActivityForm(name: "TentativeIntrusion", organisationId: 99999, id: activity.Id);
        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var db = factory.NewDbContext();
        var unchanged = await db.Activities.IgnoreQueryFilters()
            .FirstAsync(a => a.Id == activity.Id);
        unchanged.Name.Should().Be("NomProtege");
    }

    [Fact]
    public async Task EditPost_EndDateBeforeStartDate_ReturnsOkAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "DatesValides");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = ActivityForm(
            name: "DatesInverseesEdit",
            startDate: "2026-07-10",
            endDate: "2026-07-01",
            organisationId: org.Id,
            id: activity.Id);
        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var unchanged = await db.Activities.IgnoreQueryFilters()
            .FirstAsync(a => a.Id == activity.Id);
        unchanged.Name.Should().Be("DatesValides");
    }

    // ---------- GET Delete ----------

    [Fact]
    public async Task DeleteForm_ExistingActivity_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageDeleteGet");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Activities/Delete/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("StageDeleteGet");
    }

    [Fact]
    public async Task DeleteForm_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, "Coordinator");
        var response = await client.GetAsync("/Activities/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePost_ActivityInAnotherOrganisation_ReturnsNotFoundAndDoesNotRemove()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "NeSupprimePas");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = activity.Id.ToString() });
        var response = await client.PostAsync($"/Activities/Delete/{activity.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var db = factory.NewDbContext();
        var stillThere = await db.Activities.IgnoreQueryFilters()
            .AnyAsync(a => a.Id == activity.Id);
        stillThere.Should().BeTrue();
    }

    // ---------- Export: Excel ----------

    [Fact]
    public async Task Export_Excel_ReturnsNonEmptySpreadsheet()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.AddRange(org, TestData.Activity(org, "StageExportXls"));
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Activities/Export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_Excel_WithFilters_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var inactive = TestData.Activity(org, "StageInactifExport");
            inactive.IsActive = false;
            ctx.AddRange(org, TestData.Activity(org, "StageActifExport"), inactive);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        // Exercises both filter branches (searchTerm + showActiveOnly) of the Export action.
        var response = await client.GetAsync("/Activities/Export?searchTerm=Stage&showActiveOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    // ---------- Export: PDF ----------

    [Fact]
    public async Task ExportPdf_ReturnsNonEmptyPdf()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.AddRange(org, TestData.Activity(org, "StageExportPdf"));
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Activities/ExportPdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportPdf_WithSearchTerm_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.AddRange(org, TestData.Activity(org, "StagePdfFiltre"));
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Activities/ExportPdf?searchTerm=StagePdfFiltre");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    // ---------- Authentication on additional endpoints ----------

    [Fact]
    public async Task Export_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Activities/Export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExportPdf_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Activities/ExportPdf");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EditForm_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Activities/Edit/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
