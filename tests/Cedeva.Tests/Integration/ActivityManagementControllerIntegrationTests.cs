using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration tests for <c>ActivityManagementController</c>. This controller is an
/// activity-management dashboard (no classic Create CRUD): it exposes activity-scoped views
/// (Index, Presences, SendEmail, SentEmails, TeamMembers, ManageBookings, GroupAssignment, Print)
/// and mutating POST endpoints (ConfirmBooking, Add/RemoveTeamMember, UpdatePresence,
/// AssignToGroup, UpdateBooking). The activity is always resolved through the
/// multi-tenancy query filter, so a Coordinator of another organisation gets NotFound.
/// </summary>
[Collection("WebApp")]
public class ActivityManagementControllerIntegrationTests
{
    private const int ForeignOrgId = 99999;

    private static TeamMember TeamMember(Organisation org, string firstName = "Tom", string lastName = "Teamer") => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = $"{firstName}.{lastName}@test.be".ToLowerInvariant(),
        BirthDate = new DateTime(1990, 1, 1),
        Address = TestData.Address(),
        MobilePhoneNumber = "0470000000",
        NationalRegisterNumber = "90010112345",
        TeamRole = TeamRole.Animator,
        License = License.License,
        Status = Status.Volunteer,
        LicenseUrl = "https://example.test/license.pdf",
        Organisation = org
    };

    // ---------------------------------------------------------------------
    // Authentication / authorization
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/ActivityManagement/Index?id=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // GET Index
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithActivityInOwnOrganisation_RendersDashboard()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Gestion");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Index?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stage Gestion");
    }

    [Fact]
    public async Task Index_WithUnknownActivityId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/Index?id=987654");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Index_WithActivityInAnotherOrganisation_ReturnsNotFound()
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

        // Coordinator of a different org: query filter hides the activity -> NotFound.
        var client = factory.CreateClientFor("u1", ForeignOrgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Index?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Index_AsAdmin_BypassesTenantFilter()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Admin");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync($"/ActivityManagement/Index?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Stage Admin");
    }

    // ---------------------------------------------------------------------
    // GET TeamMembers / SentEmails
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TeamMembers_WithActivityInOwnOrganisation_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity, TeamMember(org));
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/TeamMembers?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SentEmails_WithUnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/SentEmails?id=555");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // POST ConfirmBooking
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ConfirmBooking_WithGroup_ConfirmsAndAssignsGroup_AndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        int bookingId = 0;
        int groupId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var group = TestData.Group(activity, "Les Lions");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, totalAmount: 100m, paidAmount: 0m);
            booking.IsConfirmed = false;
            ctx.AddRange(org, activity, group, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            groupId = group.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["bookingId"] = bookingId.ToString(),
            ["groupId"] = groupId.ToString()
        });

        var response = await client.PostAsync("/ActivityManagement/ConfirmBooking", content);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var saved = await db.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == bookingId);
        saved.IsConfirmed.Should().BeTrue();
        saved.GroupId.Should().Be(groupId);
    }

    [Fact]
    public async Task ConfirmBooking_WithoutGroup_CreatesDefaultGroup_AndConfirms()
    {
        using var factory = new CedevaWebApplicationFactory();
        int bookingId = 0;
        int activityId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, totalAmount: 100m, paidAmount: 0m);
            booking.IsConfirmed = false;
            ctx.AddRange(org, activity, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            activityId = activity.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["bookingId"] = bookingId.ToString()
        });

        var response = await client.PostAsync("/ActivityManagement/ConfirmBooking", content);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var saved = await db.Bookings.IgnoreQueryFilters().Include(b => b.Group).FirstAsync(b => b.Id == bookingId);
        saved.IsConfirmed.Should().BeTrue();
        saved.GroupId.Should().NotBeNull();
        saved.Group!.Label.Should().Be("Sans groupe");

        var defaultGroups = await db.ActivityGroups.IgnoreQueryFilters()
            .Where(g => g.ActivityId == activityId && g.Label == "Sans groupe").ToListAsync();
        defaultGroups.Should().HaveCount(1);
    }

    [Fact]
    public async Task ConfirmBooking_WithUnknownBooking_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["bookingId"] = "424242"
        });

        var response = await client.PostAsync("/ActivityManagement/ConfirmBooking", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // POST Add / Remove TeamMember
    // ---------------------------------------------------------------------

    [Fact]
    public async Task AddTeamMember_AssignsMember_AndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;
        int teamMemberId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var tm = TeamMember(org);
            ctx.AddRange(org, activity, tm);
            ctx.SaveChanges();
            activityId = activity.Id;
            teamMemberId = tm.TeamMemberId;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = activityId.ToString(),
            ["teamMemberId"] = teamMemberId.ToString()
        });

        var response = await client.PostAsync("/ActivityManagement/AddTeamMember", content);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var activity = await db.Activities.IgnoreQueryFilters()
            .Include(a => a.TeamMembers).FirstAsync(a => a.Id == activityId);
        activity.TeamMembers.Select(tm => tm.TeamMemberId).Should().Contain(teamMemberId);
    }

    [Fact]
    public async Task AddTeamMember_WithUnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "777777",
            ["teamMemberId"] = "1"
        });

        var response = await client.PostAsync("/ActivityManagement/AddTeamMember", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveTeamMember_UnassignsMember_AndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;
        int teamMemberId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var tm = TeamMember(org);
            activity.TeamMembers.Add(tm);
            ctx.AddRange(org, activity, tm);
            ctx.SaveChanges();
            activityId = activity.Id;
            teamMemberId = tm.TeamMemberId;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = activityId.ToString(),
            ["teamMemberId"] = teamMemberId.ToString()
        });

        var response = await client.PostAsync("/ActivityManagement/RemoveTeamMember", content);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var activity = await db.Activities.IgnoreQueryFilters()
            .Include(a => a.TeamMembers).FirstAsync(a => a.Id == activityId);
        activity.TeamMembers.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------
    // POST UpdatePresence (JSON result)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdatePresence_WithValidBookingDay_PersistsPresence()
    {
        using var factory = new CedevaWebApplicationFactory();
        int bookingDayId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var day = new ActivityDay { Label = "Lundi", DayDate = new DateTime(2026, 7, 6), IsActive = true, Activity = activity };
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            var bookingDay = new BookingDay { ActivityDay = day, Booking = booking, IsReserved = true, IsPresent = false };
            ctx.AddRange(org, activity, day, parent, child, booking, bookingDay);
            ctx.SaveChanges();
            bookingDayId = bookingDay.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["bookingDayId"] = bookingDayId.ToString(),
            ["isPresent"] = "true"
        });

        var response = await client.PostAsync("/ActivityManagement/UpdatePresence", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");

        using var db = factory.NewDbContext();
        var saved = await db.BookingDays.IgnoreQueryFilters().FirstAsync(bd => bd.Id == bookingDayId);
        saved.IsPresent.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePresence_WithUnknownBookingDay_ReturnsSuccessFalseJson()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["bookingDayId"] = "888888",
            ["isPresent"] = "true"
        });

        var response = await client.PostAsync("/ActivityManagement/UpdatePresence", content);

        // Action returns a 200 Json payload with success=false when the booking-day is missing.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    // ---------------------------------------------------------------------
    // POST AssignToGroup (JSON body -> JSON result)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task AssignToGroup_WithValidBookingAndGroup_PersistsAndReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        int bookingId = 0;
        int groupId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var group = TestData.Group(activity, "Les Tigres");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            ctx.AddRange(org, activity, group, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            groupId = group.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/AssignToGroup",
            new { BookingId = bookingId, GroupId = groupId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");

        using var db = factory.NewDbContext();
        var saved = await db.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == bookingId);
        saved.GroupId.Should().Be(groupId);
    }

    [Fact]
    public async Task AssignToGroup_WithUnknownBooking_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/AssignToGroup",
            new { BookingId = 999111, GroupId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    // ---------------------------------------------------------------------
    // POST UpdateBooking (JSON body -> JSON result)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdateBooking_SetsMedicalSheet_AndPersists()
    {
        using var factory = new CedevaWebApplicationFactory();
        int bookingId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            booking.IsMedicalSheet = false;
            ctx.AddRange(org, activity, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/UpdateBooking",
            new { BookingId = bookingId, IsMedicalSheet = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");

        using var db = factory.NewDbContext();
        var saved = await db.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == bookingId);
        saved.IsMedicalSheet.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateBooking_WithUnknownBooking_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/UpdateBooking",
            new { BookingId = 654321, IsConfirmed = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // GET GetManageBookingsStats (anonymous JSON result)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetManageBookingsStats_CountsBookingsNeedingAttention()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);

            // Unconfirmed, no group, no medical sheet -> counts in all three buckets.
            var b1 = TestData.Booking(child, activity, null, 100m, 0m);
            b1.IsConfirmed = false;
            b1.IsMedicalSheet = false;

            ctx.AddRange(org, activity, parent, child, b1);
            ctx.SaveChanges();
            activityId = activity.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/GetManageBookingsStats?activityId={activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("pendingConfirmation").GetInt32().Should().Be(1);
        root.GetProperty("withoutGroup").GetInt32().Should().Be(1);
        root.GetProperty("withoutMedicalSheet").GetInt32().Should().Be(1);
    }

    // ---------------------------------------------------------------------
    // GET Print
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Print_WithUnknownActivityOrDay_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/Print?activityId=123&dayId=456");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Print_WithValidActivityAndDay_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;
        int dayId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var day = new ActivityDay { Label = "Mardi", DayDate = new DateTime(2026, 7, 7), IsActive = true, Activity = activity };
            ctx.AddRange(org, activity, day);
            ctx.SaveChanges();
            activityId = activity.Id;
            dayId = day.DayId;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Print?activityId={activityId}&dayId={dayId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
