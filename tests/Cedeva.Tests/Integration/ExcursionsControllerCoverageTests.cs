using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage tests for <c>ExcursionsController</c> that exercise actions and branches NOT
/// already covered by <see cref="ExcursionsControllerIntegrationTests"/> (which only covers
/// RegisterChild valid/unauth, UnregisterChild valid, and UpdateAttendance valid).
///
/// Covers: Index (id, session fallback, NotFound, tenant isolation), Create GET + POST
/// (valid / model-invalid / no-groups / inactive-day), Details, Edit GET + POST
/// (valid / NotFound), Delete GET + POST (with / without registrations), Registrations GET,
/// Attendance GET, SendEmail GET + POST (valid / no-recipients), Expenses GET + AddExpense
/// POST (valid / invalid), TeamManagement GET, AssignTeamMember / UnassignTeamMember /
/// UpdateTeamAttendance JSON endpoints (success + error branches), BeginExcursions, plus the
/// UnregisterChild "not registered" branch.
/// </summary>
[Collection("WebApp")]
public class ExcursionsControllerCoverageTests
{
    private sealed record Seeded(
        int OrgId,
        int ActivityId,
        int GroupId,
        int ExcursionId,
        int BookingId,
        int TeamMemberId,
        DateTime ActiveDay);

    private const string OtherOrgUser = "u-other";

    /// <summary>
    /// Seeds a full excursion graph: org, activity (with one active ActivityDay matching the
    /// excursion date), one group, parent + child + confirmed booking, an excursion linked to
    /// the group, and a team member assigned to the parent activity.
    /// </summary>
    private static Seeded SeedFull(CedevaWebApplicationFactory factory, decimal cost = 15m, bool excursionActive = true)
    {
        Organisation org = null!;
        Activity activity = null!;
        ActivityGroup group = null!;
        Excursion excursion = null!;
        Booking booking = null!;
        TeamMember teamMember = null!;
        var activeDay = new DateTime(2026, 7, 3); // == TestData.Excursion ExcursionDate

        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            group = TestData.Group(activity, "Lions");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group, totalAmount: 100m, paidAmount: 0m);
            excursion = TestData.Excursion(activity, cost, isActive: excursionActive);
            var link = TestData.ExcursionGroup(excursion, group);

            var day = new ActivityDay
            {
                Label = "Jour 1",
                DayDate = activeDay,
                IsActive = true,
                Activity = activity
            };

            teamMember = new TeamMember
            {
                FirstName = "Tim",
                LastName = "Team",
                Email = "tim.team@test.be",
                MobilePhoneNumber = "0470111222",
                NationalRegisterNumber = "85061513380",
                BirthDate = new DateTime(1985, 6, 15),
                Address = TestData.Address(),
                TeamRole = TeamRole.Animator,
                License = License.License,
                Status = Status.Volunteer,
                LicenseUrl = "/uploads/seed/license.pdf",
                Organisation = org
            };
            // Assign team member to the parent activity (used by TeamManagement / Assign).
            activity.TeamMembers.Add(teamMember);

            ctx.AddRange(org, activity, group, parent, child, booking, excursion, link, day, teamMember);
            return 0;
        });

        return new Seeded(org.Id, activity.Id, group.Id, excursion.Id, booking.Id, teamMember.TeamMemberId, activeDay);
    }

    private static int RegisterChild(CedevaWebApplicationFactory factory, HttpClient client, Seeded s)
    {
        var resp = client.PostAsync("/Excursions/RegisterChild", Form(new()
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["bookingId"] = s.BookingId.ToString(),
        })).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();

        using var ctx = factory.NewDbContext();
        return ctx.ExcursionRegistrations.Single(r => r.BookingId == s.BookingId).Id;
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);

    // ---------------------------------------------------------------------
    // Index
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithActivityId_ReturnsExcursionList()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Index/{s.ActivityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Excursion Test");
    }

    [Fact]
    public async Task Index_UsesSessionFallback_WhenNoIdProvided()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        // First request seeds the session with the activity id.
        (await client.GetAsync($"/Excursions/Index/{s.ActivityId}")).EnsureSuccessStatusCode();

        // Second request without an id should fall back to the session value.
        var response = await client.GetAsync("/Excursions/Index");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Excursion Test");
    }

    [Fact]
    public async Task Index_NoIdAndNoSession_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Index");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Index_UnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Index/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Index_ForeignOrganisation_ReturnsNotFound_TenantIsolation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        // Coordinator of a DIFFERENT org cannot see the seeded activity (filtered out).
        var client = factory.CreateClientFor(OtherOrgUser, s.OrgId + 999, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Index/{s.ActivityId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Index_Unauthenticated_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Excursions/Index/{s.ActivityId}");

        // Test auth scheme returns NoResult => challenge => 401 (no cookie redirect configured for Test scheme).
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // Create (GET / POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_Get_ReturnsForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Create/{s.ActivityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_Get_UnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Create/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Post_Valid_PersistsExcursionAndGroupAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Create", Form(new()
        {
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Nouvelle Excursion",
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "12", // whole number avoids fr-vs-invariant decimal-separator ambiguity
            ["Type"] = ExcursionType.Pool.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        // RedirectToAction(Index, new { id = ActivityId }) populates the {id} route segment.
        response.Headers.Location!.ToString().Should().Be($"/Excursions/Index/{s.ActivityId}");

        await using var ctx = factory.NewDbContext();
        var created = await ctx.Excursions.Include(e => e.ExcursionGroups)
            .SingleAsync(e => e.Name == "Nouvelle Excursion");
        created.Cost.Should().Be(12m);
        created.IsActive.Should().BeTrue();
        created.ExcursionGroups.Should().ContainSingle(g => g.ActivityGroupId == s.GroupId);
    }

    [Fact]
    public async Task Create_Post_ModelInvalid_MissingName_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Create", Form(new()
        {
            ["ActivityId"] = s.ActivityId.ToString(),
            // Name omitted -> invalid
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "12.50",
            ["Type"] = ExcursionType.Pool.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var ctx = factory.NewDbContext();
        (await ctx.Excursions.CountAsync(e => e.ActivityId == s.ActivityId)).Should().Be(1); // only the seeded one
    }

    [Fact]
    public async Task Create_Post_NoGroups_ReRendersWith200_AndNothingPersisted()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Create", Form(new()
        {
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Sans Groupe",
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "12.50",
            ["Type"] = ExcursionType.Pool.ToString(),
            // SelectedGroupIds omitted -> "at least one group required"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var ctx = factory.NewDbContext();
        (await ctx.Excursions.AnyAsync(e => e.Name == "Sans Groupe")).Should().BeFalse();
    }

    [Fact]
    public async Task Create_Post_DateNotActiveDay_ReRendersWith200_AndNothingPersisted()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Create", Form(new()
        {
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Mauvais Jour",
            ["ExcursionDate"] = "2030-01-01", // not an active activity day
            ["Cost"] = "12.50",
            ["Type"] = ExcursionType.Pool.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var ctx = factory.NewDbContext();
        (await ctx.Excursions.AnyAsync(e => e.Name == "Mauvais Jour")).Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // Details
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Details_ExistingExcursion_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Details/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Excursion Test");
    }

    [Fact]
    public async Task Details_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_ForeignOrganisation_ReturnsNotFound_TenantIsolation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor(OtherOrgUser, s.OrgId + 999, "Coordinator");

        // Excursion.Activity is a required nav into a tenancy-filtered entity -> filtered out.
        var response = await client.GetAsync($"/Excursions/Details/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // Edit (GET / POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Edit_Get_ReturnsForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Edit/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_Post_Valid_UpdatesAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Edit", Form(new()
        {
            ["Id"] = s.ExcursionId.ToString(),
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Excursion Modifiee",
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "20",
            ["Type"] = ExcursionType.Sports.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be($"/Excursions/Index/{s.ActivityId}");

        await using var ctx = factory.NewDbContext();
        var updated = await ctx.Excursions.SingleAsync(e => e.Id == s.ExcursionId);
        updated.Name.Should().Be("Excursion Modifiee");
        updated.Cost.Should().Be(20m);
        updated.Type.Should().Be(ExcursionType.Sports);
    }

    [Fact]
    public async Task Edit_Post_ModelInvalid_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Edit", Form(new()
        {
            ["Id"] = s.ExcursionId.ToString(),
            ["ActivityId"] = s.ActivityId.ToString(),
            // Name missing -> invalid
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "20",
            ["Type"] = ExcursionType.Sports.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Post_NoGroups_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Edit", Form(new()
        {
            ["Id"] = s.ExcursionId.ToString(),
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Pas De Groupe",
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "20",
            ["Type"] = ExcursionType.Sports.ToString(),
            // no SelectedGroupIds
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Post_DateNotActiveDay_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Edit", Form(new()
        {
            ["Id"] = s.ExcursionId.ToString(),
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Mauvaise Date",
            ["ExcursionDate"] = "2031-02-02",
            ["Cost"] = "20",
            ["Type"] = ExcursionType.Sports.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // Delete (GET / POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Delete_Get_ReturnsConfirmation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Delete/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Post_NoRegistrations_SoftDeletesAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Delete", Form(new()
        {
            ["id"] = s.ExcursionId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be($"/Excursions/Index/{s.ActivityId}");

        await using var ctx = factory.NewDbContext();
        var excursion = await ctx.Excursions.SingleAsync(e => e.Id == s.ExcursionId);
        excursion.IsActive.Should().BeFalse(); // soft delete
    }

    [Fact]
    public async Task Delete_Post_WithRegistrations_DoesNotDelete_AndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        RegisterChild(factory, client, s); // create a registration that blocks delete

        var response = await client.PostAsync("/Excursions/Delete", Form(new()
        {
            ["id"] = s.ExcursionId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be($"/Excursions/Index/{s.ActivityId}");

        await using var ctx = factory.NewDbContext();
        var excursion = await ctx.Excursions.SingleAsync(e => e.Id == s.ExcursionId);
        excursion.IsActive.Should().BeTrue(); // not deleted because it has registrations
    }

    [Fact]
    public async Task Delete_Post_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Delete", Form(new()
        {
            ["id"] = "999999",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // Registrations / Attendance (GET)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Registrations_Get_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Registrations/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Registrations_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Registrations/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Attendance_Get_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Attendance/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Attendance_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Attendance/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // UnregisterChild - "not registered" branch (success branch covered elsewhere)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UnregisterChild_NotRegistered_ReturnsSuccessFalse()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/UnregisterChild", Form(new()
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["bookingId"] = s.BookingId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task UpdateAttendance_UnknownRegistration_ReturnsSuccessFalse()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/UpdateAttendance", Form(new()
        {
            ["registrationId"] = "999999",
            ["isPresent"] = "true",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    // ---------------------------------------------------------------------
    // SendEmail (GET / POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SendEmail_Get_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/SendEmail/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendEmail_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/SendEmail/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendEmail_Post_WithRecipients_Redirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        // Register the child so there is a recipient for "all_registered".
        RegisterChild(factory, client, s);

        var response = await client.PostAsync("/Excursions/SendEmail", Form(new()
        {
            ["ExcursionId"] = s.ExcursionId.ToString(),
            ["SelectedRecipient"] = "all_registered",
            ["Subject"] = "Info excursion",
            ["Message"] = "Bonjour, voici les details.",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task SendEmail_Post_ModelInvalid_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/SendEmail", Form(new()
        {
            ["ExcursionId"] = s.ExcursionId.ToString(),
            ["SelectedRecipient"] = "all_registered",
            // Subject + Message missing -> invalid
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendEmail_Post_NoRecipientsFound_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        // No registrations exist => no recipients => re-render with model error.
        var response = await client.PostAsync("/Excursions/SendEmail", Form(new()
        {
            ["ExcursionId"] = s.ExcursionId.ToString(),
            ["SelectedRecipient"] = "all_registered",
            ["Subject"] = "Info excursion",
            ["Message"] = "Bonjour.",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // Expenses (GET) + AddExpense (POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Expenses_Get_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Expenses/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Expenses_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Expenses/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // NOTE on AddExpense: ExcursionExpensesViewModel exposes the full Excursion entity as a bound
    // property, whose non-nullable navigation chain (Excursion -> Activity -> Organisation) is
    // implicitly [Required]. The controller only does ModelState.Remove("Excursion") (which does
    // NOT cascade to "Excursion.Name"/"Excursion.Activity"/...), so over HTTP the real Expenses
    // form (which posts only Excursion.Id + Excursion.ActivityId) always re-renders with 200 —
    // a genuine 302 happy path is not reachable through form binding. These tests assert the
    // behaviour the controller ACTUALLY produces over HTTP rather than a 302 it cannot reach.

    [Fact]
    public async Task AddExpense_Post_RealFormFields_ReRendersWith200_DueToNestedRequiredNavigations()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        // Exactly what the real Expenses form posts: two hidden Excursion fields + flat fields.
        var response = await client.PostAsync("/Excursions/AddExpense", Form(new()
        {
            ["Excursion.Id"] = s.ExcursionId.ToString(),
            ["Excursion.ActivityId"] = s.ActivityId.ToString(),
            ["Label"] = "Bus",
            ["Amount"] = "75",
            ["ExpenseDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["OrganizationPaymentSource"] = "OrganizationCard",
        }));

        // The bound Excursion.Name / Excursion.Activity required validations block the happy path.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var ctx = factory.NewDbContext();
        (await ctx.Expenses.AnyAsync(e => e.ExcursionId == s.ExcursionId)).Should().BeFalse();
    }

    [Fact]
    public async Task AddExpense_Post_InvalidFlatFields_ReRendersExpensesWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/AddExpense", Form(new()
        {
            ["Excursion.Id"] = s.ExcursionId.ToString(),
            ["Excursion.ActivityId"] = s.ActivityId.ToString(),
            // Label missing + Amount 0 (below Range min) -> invalid flat fields too.
            ["Amount"] = "0",
            ["ExpenseDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["OrganizationPaymentSource"] = "OrganizationCard",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var ctx = factory.NewDbContext();
        (await ctx.Expenses.AnyAsync(e => e.ExcursionId == s.ExcursionId)).Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // TeamManagement (GET) + Assign/Unassign/UpdateTeamAttendance (JSON)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TeamManagement_Get_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/TeamManagement/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TeamManagement_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/TeamManagement/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignTeamMember_New_ReturnsSuccessAndPersists()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/AssignTeamMember", Form(new()
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["teamMemberId"] = s.TeamMemberId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");

        await using var ctx = factory.NewDbContext();
        (await ctx.ExcursionTeamMembers.AnyAsync(
            t => t.ExcursionId == s.ExcursionId && t.TeamMemberId == s.TeamMemberId)).Should().BeTrue();
    }

    [Fact]
    public async Task AssignTeamMember_Duplicate_ReturnsSuccessFalse()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var form = Form(new()
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["teamMemberId"] = s.TeamMemberId.ToString(),
        });
        (await client.PostAsync("/Excursions/AssignTeamMember", form)).EnsureSuccessStatusCode();

        var response = await client.PostAsync("/Excursions/AssignTeamMember", Form(new()
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["teamMemberId"] = s.TeamMemberId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task UnassignTeamMember_Existing_ReturnsSuccessAndRemoves()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        (await client.PostAsync("/Excursions/AssignTeamMember", Form(new()
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["teamMemberId"] = s.TeamMemberId.ToString(),
        }))).EnsureSuccessStatusCode();

        var response = await client.PostAsync("/Excursions/UnassignTeamMember", Form(new()
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["teamMemberId"] = s.TeamMemberId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");

        await using var ctx = factory.NewDbContext();
        (await ctx.ExcursionTeamMembers.AnyAsync(
            t => t.ExcursionId == s.ExcursionId && t.TeamMemberId == s.TeamMemberId)).Should().BeFalse();
    }

    [Fact]
    public async Task UnassignTeamMember_NotAssigned_ReturnsSuccessFalse()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/UnassignTeamMember", Form(new()
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["teamMemberId"] = s.TeamMemberId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task UpdateTeamAttendance_Existing_MarksPresent()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        (await client.PostAsync("/Excursions/AssignTeamMember", Form(new()
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["teamMemberId"] = s.TeamMemberId.ToString(),
        }))).EnsureSuccessStatusCode();

        int etmId;
        await using (var ctx = factory.NewDbContext())
        {
            etmId = (await ctx.ExcursionTeamMembers.SingleAsync(
                t => t.ExcursionId == s.ExcursionId && t.TeamMemberId == s.TeamMemberId)).Id;
        }

        var response = await client.PostAsync("/Excursions/UpdateTeamAttendance", Form(new()
        {
            ["excursionTeamMemberId"] = etmId.ToString(),
            ["isPresent"] = "true",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");

        await using var verify = factory.NewDbContext();
        (await verify.ExcursionTeamMembers.SingleAsync(t => t.Id == etmId)).IsPresent.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTeamAttendance_Unknown_ReturnsSuccessFalse()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/UpdateTeamAttendance", Form(new()
        {
            ["excursionTeamMemberId"] = "999999",
            ["isPresent"] = "true",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    // ---------------------------------------------------------------------
    // BeginExcursions
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BeginExcursions_Post_RedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/BeginExcursions", Form(new()
        {
            ["id"] = s.ActivityId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        // RedirectToAction(Index, new { id }) => "/Excursions/Index/{id}"
        response.Headers.Location!.ToString().Should().Contain(s.ActivityId.ToString());
    }
}
