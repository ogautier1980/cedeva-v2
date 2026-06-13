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
/// Further integration coverage for <c>ActivityManagementController</c>, targeting
/// actions/branches the two existing suites do NOT exercise:
/// - SendEmail POST invalid-model re-render and SendEmail GET unauthenticated,
/// - Presences with an explicit dayId, no-id/no-session NotFound and tenant isolation,
/// - session-driven GroupAssignment / ManageBookings NotFound and tenant-isolation branches,
/// - UpdatePresence persisting <c>false</c>,
/// - unauthenticated access to the JSON/AJAX endpoints (AssignToGroup, UpdateBooking,
///   GetManageBookingsStats, UpdatePresence),
/// - tenant isolation / empty results on GetManageBookingsStats and SentEmails.
/// </summary>
[Collection("WebApp")]
public class ActivityManagementControllerMoreTests
{
    private const int ForeignOrgId = 99999;

    private static ActivityDay Day(Activity activity, string label, DateTime date, bool isActive = true) => new()
    {
        Label = label,
        DayDate = date,
        IsActive = isActive,
        Activity = activity
    };

    // ---------------------------------------------------------------------
    // GET SendEmail - unauthenticated
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SendEmail_Get_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/ActivityManagement/SendEmail?id=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SendEmail_Get_WithActivityInAnotherOrganisation_ReturnsNotFound()
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

        var client = factory.CreateClientFor("u1", ForeignOrgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/SendEmail?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // POST SendEmail - invalid model (missing required fields) re-renders the form (200)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SendEmail_Post_WithMissingRequiredFields_RerendersForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Email Invalid");
            var group = TestData.Group(activity, "Les Phoques");
            var day = Day(activity, "Lundi", new DateTime(2026, 7, 6));
            var excursion = TestData.Excursion(activity, 10m);
            ctx.AddRange(org, activity, group, day, excursion);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        // Subject, Message and SelectedRecipient are [Required] -> ModelState invalid.
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = activity.Id.ToString()
        });

        var response = await client.PostAsync("/ActivityManagement/SendEmail", content);

        // Invalid POST re-renders the view (no redirect, no email sent).
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var sent = await db.EmailsSent.IgnoreQueryFilters()
            .Where(e => e.ActivityId == activity.Id).ToListAsync();
        sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendEmail_Post_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = "1",
            ["SelectedRecipient"] = "allparents",
            ["Subject"] = "Hi",
            ["Message"] = "Body"
        });

        var response = await client.PostAsync("/ActivityManagement/SendEmail", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // GET Presences - explicit dayId, no-id/no-session, tenant isolation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Presences_WithExplicitDayId_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int dayId = 0;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Presence Day");
            var day = Day(activity, "Mardi", new DateTime(2026, 7, 7));
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m); // confirmed by default
            var bookingDay = new BookingDay { ActivityDay = day, Booking = booking, IsReserved = true, IsPresent = false };
            ctx.AddRange(org, activity, day, parent, child, booking, bookingDay);
            ctx.SaveChanges();
            dayId = day.DayId;
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Presences?id={activity.Id}&dayId={dayId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stage Presence Day");
        // The confirmed child appears in the presence list (last name is ASCII-safe).
        html.Should().Contain("Enfant");
    }

    [Fact]
    public async Task Presences_WithNoIdAndNoSession_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/Presences");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Presences_WithActivityInAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            var day = Day(activity, "Lundi", new DateTime(2026, 7, 6));
            ctx.AddRange(org, activity, day);
            return 0;
        });

        var client = factory.CreateClientFor("u1", ForeignOrgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Presences?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // GET GroupAssignment - session points at a foreign-org activity (tenant isolation)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GroupAssignment_WhenSelectedActivityIsForeign_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Foreign Groups");
            ctx.AddRange(org, activity);
            return 0;
        });

        // Coordinator of a DIFFERENT org. The Begin* POST blindly stores the id in session,
        // but the subsequent GET resolves the activity through the tenant filter -> NotFound.
        var client = factory.CreateClientFor("u1", ForeignOrgId, "Coordinator");

        var begin = await client.PostAsync("/ActivityManagement/BeginGroupAssignment",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = activity.Id.ToString() }));
        begin.StatusCode.Should().Be(HttpStatusCode.Found);

        var response = await client.GetAsync("/ActivityManagement/GroupAssignment");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GroupAssignment_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/ActivityManagement/GroupAssignment");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // GET ManageBookings - session points at a foreign-org activity (tenant isolation)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ManageBookings_WhenSelectedActivityIsForeign_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Foreign Manage");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", ForeignOrgId, "Coordinator");

        var begin = await client.PostAsync("/ActivityManagement/BeginManageBookings",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = activity.Id.ToString() }));
        begin.StatusCode.Should().Be(HttpStatusCode.Found);

        var response = await client.GetAsync("/ActivityManagement/ManageBookings");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // GET SentEmails - tenant isolation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SentEmails_WithActivityInAnotherOrganisation_ReturnsNotFound()
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

        var client = factory.CreateClientFor("u1", ForeignOrgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/SentEmails?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // POST UpdatePresence - persists IsPresent = false
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdatePresence_SettingFalse_PersistsAbsence()
    {
        using var factory = new CedevaWebApplicationFactory();
        int bookingDayId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var day = Day(activity, "Mercredi", new DateTime(2026, 7, 8));
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            var bookingDay = new BookingDay { ActivityDay = day, Booking = booking, IsReserved = true, IsPresent = true };
            ctx.AddRange(org, activity, day, parent, child, booking, bookingDay);
            ctx.SaveChanges();
            bookingDayId = bookingDay.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["bookingDayId"] = bookingDayId.ToString(),
            ["isPresent"] = "false"
        });

        var response = await client.PostAsync("/ActivityManagement/UpdatePresence", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");

        using var db = factory.NewDbContext();
        var saved = await db.BookingDays.IgnoreQueryFilters().FirstAsync(bd => bd.Id == bookingDayId);
        saved.IsPresent.Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePresence_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["bookingDayId"] = "1",
            ["isPresent"] = "true"
        });

        var response = await client.PostAsync("/ActivityManagement/UpdatePresence", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // JSON/AJAX endpoints - unauthenticated access is challenged
    // ---------------------------------------------------------------------

    [Fact]
    public async Task AssignToGroup_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.PostAsJsonAsync("/ActivityManagement/AssignToGroup",
            new { BookingId = 1, GroupId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateBooking_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.PostAsJsonAsync("/ActivityManagement/UpdateBooking",
            new { BookingId = 1, IsConfirmed = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetManageBookingsStats_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/ActivityManagement/GetManageBookingsStats?activityId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // GET GetManageBookingsStats - cross-tenant activity is filtered out (empty -> zeros)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetManageBookingsStats_ForForeignActivity_ReturnsZeros()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            // A booking needing attention, but it belongs to another org.
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            booking.IsConfirmed = false;
            booking.IsMedicalSheet = false;
            ctx.AddRange(org, activity, parent, child, booking);
            ctx.SaveChanges();
            activityId = activity.Id;
            return 0;
        });

        // Coordinator of a foreign org: the tenant filter hides the bookings -> all zeros.
        var client = factory.CreateClientFor("u1", ForeignOrgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/GetManageBookingsStats?activityId={activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("pendingConfirmation").GetInt32().Should().Be(0);
        root.GetProperty("withoutGroup").GetInt32().Should().Be(0);
        root.GetProperty("withoutMedicalSheet").GetInt32().Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // GET GetManageBookingsStats - distinct buckets (group OK + medical OK, only unconfirmed)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetManageBookingsStats_OnlyPendingConfirmation_CountsSingleBucket()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var group = TestData.Group(activity, "Les Pumas");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            // Has a real group + medical sheet, but not confirmed -> only pendingConfirmation.
            var booking = TestData.Booking(child, activity, group, 100m, 0m);
            booking.IsConfirmed = false;
            booking.IsMedicalSheet = true;
            ctx.AddRange(org, activity, group, parent, child, booking);
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
        root.GetProperty("withoutGroup").GetInt32().Should().Be(0);
        root.GetProperty("withoutMedicalSheet").GetInt32().Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // POST UpdateBooking - clearing confirmation on an already-grouped booking (not complete)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdateBooking_UnconfirmingGroupedBooking_ReportsNotComplete()
    {
        using var factory = new CedevaWebApplicationFactory();
        int bookingId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var group = TestData.Group(activity, "Les Lynx");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, group, 100m, 0m); // confirmed + grouped
            booking.IsMedicalSheet = true;
            ctx.AddRange(org, activity, group, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/UpdateBooking",
            new { BookingId = bookingId, IsConfirmed = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        // Unconfirmed => not complete even though grouped + medical sheet present.
        doc.RootElement.GetProperty("isComplete").GetBoolean().Should().BeFalse();

        using var db = factory.NewDbContext();
        var saved = await db.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == bookingId);
        saved.IsConfirmed.Should().BeFalse();
    }
}
