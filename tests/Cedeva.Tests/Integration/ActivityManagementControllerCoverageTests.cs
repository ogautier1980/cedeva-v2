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
/// Additional integration coverage for <c>ActivityManagementController</c>, exercising
/// actions and branches the original <c>ActivityManagementControllerIntegrationTests</c>
/// left untouched: the activity-scoped GET dashboards (UnconfirmedBookings, Presences,
/// SendEmail, SentEmails), the session-driven GETs (GroupAssignment, ManageBookings),
/// every "Begin*"/Index POST navigation shim, additional NotFound / not-found-branch
/// paths on the mutating endpoints, and tenant-isolation on a non-Index GET.
/// </summary>
[Collection("WebApp")]
public class ActivityManagementControllerCoverageTests
{
    private const int ForeignOrgId = 99999;

    private static TeamMember TeamMember(Organisation org, string firstName = "Tina", string lastName = "Trainer") => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = $"{firstName}.{lastName}@test.be".ToLowerInvariant(),
        BirthDate = new DateTime(1991, 2, 2),
        Address = TestData.Address(),
        MobilePhoneNumber = "0471111111",
        NationalRegisterNumber = "91020212345",
        TeamRole = TeamRole.Animator,
        License = License.License,
        Status = Status.Volunteer,
        LicenseUrl = "https://example.test/license.pdf",
        Organisation = org
    };

    private static ActivityDay Day(Activity activity, string label, DateTime date) => new()
    {
        Label = label,
        DayDate = date,
        IsActive = true,
        Activity = activity
    };

    // ---------------------------------------------------------------------
    // GET UnconfirmedBookings
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UnconfirmedBookings_WithActivityInOwnOrganisation_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Unconfirmed");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            booking.IsConfirmed = false;
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/UnconfirmedBookings?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Stage Unconfirmed");
    }

    [Fact]
    public async Task UnconfirmedBookings_WithNoIdAndNoSession_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/UnconfirmedBookings");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnconfirmedBookings_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/ActivityManagement/UnconfirmedBookings?id=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // GET Presences
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Presences_WithActivityAndDay_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Presences");
            var day = Day(activity, "Lundi", new DateTime(2026, 7, 6));
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m); // IsConfirmed=true by default
            ctx.AddRange(org, activity, day, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Presences?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Stage Presences");
    }

    [Fact]
    public async Task Presences_WithUnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/Presences?id=321321");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // GET SendEmail
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SendEmail_WithActivityInOwnOrganisation_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Email");
            var group = TestData.Group(activity, "Les Aigles");
            var day = Day(activity, "Mardi", new DateTime(2026, 7, 7));
            var excursion = TestData.Excursion(activity, 15m);
            ctx.AddRange(org, activity, group, day, excursion);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/SendEmail?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Stage Email");
    }

    [Fact]
    public async Task SendEmail_WithUnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/SendEmail?id=445566");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // GET SentEmails (happy path with rows)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SentEmails_WithActivityAndLoggedEmail_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Sent");
            ctx.AddRange(org, activity);
            ctx.SaveChanges();
            ctx.EmailsSent.Add(new EmailSent
            {
                ActivityId = activity.Id,
                RecipientType = EmailRecipient.AllParents,
                RecipientEmails = "paul.parent@test.be",
                Subject = "Bonjour",
                Message = "Message de test",
                SendSeparateEmailPerChild = false,
                SentDate = new DateTime(2026, 6, 10)
            });
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/SentEmails?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Stage Sent");
    }

    // ---------------------------------------------------------------------
    // Tenant isolation on a non-Index GET (TeamMembers)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TeamMembers_WithActivityInAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity, TeamMember(org));
            return 0;
        });

        var client = factory.CreateClientFor("u1", ForeignOrgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/TeamMembers?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // GET GroupAssignment (session-driven)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GroupAssignment_WithoutSelectedActivity_RedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/GroupAssignment");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be("/ActivityManagement");
    }

    [Fact]
    public async Task GroupAssignment_AfterSelectingActivity_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Groupes");
            var group = TestData.Group(activity, "Les Loups");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            // Confirmed booking with no group => appears as unassigned.
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            ctx.AddRange(org, activity, group, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        // Seed the session with the selected activity via the navigation POST.
        var begin = await client.PostAsync("/ActivityManagement/BeginGroupAssignment",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = activity.Id.ToString() }));
        begin.StatusCode.Should().Be(HttpStatusCode.Found);

        var response = await client.GetAsync("/ActivityManagement/GroupAssignment");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Stage Groupes");
    }

    // ---------------------------------------------------------------------
    // GET ManageBookings (session-driven)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ManageBookings_WithoutSelectedActivity_RedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/ManageBookings");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be("/ActivityManagement");
    }

    [Fact]
    public async Task ManageBookings_AfterSelectingActivity_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Manage");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            booking.IsConfirmed = false; // needs attention
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var begin = await client.PostAsync("/ActivityManagement/BeginManageBookings",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = activity.Id.ToString() }));
        begin.StatusCode.Should().Be(HttpStatusCode.Found);

        var response = await client.GetAsync("/ActivityManagement/ManageBookings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Stage Manage");
    }

    // ---------------------------------------------------------------------
    // POST navigation shims (Index + Begin*) -> 302 to their GET target
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("/ActivityManagement/Index", "/ActivityManagement")]
    [InlineData("/ActivityManagement/BeginUnconfirmedBookings", "/ActivityManagement/UnconfirmedBookings")]
    [InlineData("/ActivityManagement/BeginPresences", "/ActivityManagement/Presences")]
    [InlineData("/ActivityManagement/BeginSendEmail", "/ActivityManagement/SendEmail")]
    [InlineData("/ActivityManagement/BeginSentEmails", "/ActivityManagement/SentEmails")]
    [InlineData("/ActivityManagement/BeginTeamMembers", "/ActivityManagement/TeamMembers")]
    [InlineData("/ActivityManagement/BeginManageBookings", "/ActivityManagement/ManageBookings")]
    [InlineData("/ActivityManagement/BeginGroupAssignment", "/ActivityManagement/GroupAssignment")]
    public async Task NavigationPost_RedirectsToTargetAction(string url, string expectedLocation)
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = activity.Id.ToString() }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be(expectedLocation);
    }

    // ---------------------------------------------------------------------
    // POST AddTeamMember - team member not found branch (still redirects)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task AddTeamMember_WithUnknownTeamMember_RedirectsWithoutAssigning()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = activity.Id.ToString(),
            ["teamMemberId"] = "1234567"
        });

        var response = await client.PostAsync("/ActivityManagement/AddTeamMember", content);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be("/ActivityManagement/TeamMembers");

        using var db = factory.NewDbContext();
        var saved = await db.Activities.IgnoreQueryFilters()
            .Include(a => a.TeamMembers).FirstAsync(a => a.Id == activity.Id);
        saved.TeamMembers.Should().BeEmpty();
    }

    [Fact]
    public async Task AddTeamMember_WhenAlreadyAssigned_RemainsSingleAssignment()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int teamMemberId = 0;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            var tm = TeamMember(org);
            activity.TeamMembers.Add(tm);
            ctx.AddRange(org, activity, tm);
            ctx.SaveChanges();
            teamMemberId = tm.TeamMemberId;
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = activity.Id.ToString(),
            ["teamMemberId"] = teamMemberId.ToString()
        });

        var response = await client.PostAsync("/ActivityManagement/AddTeamMember", content);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var saved = await db.Activities.IgnoreQueryFilters()
            .Include(a => a.TeamMembers).FirstAsync(a => a.Id == activity.Id);
        saved.TeamMembers.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveTeamMember_WithUnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "888999",
            ["teamMemberId"] = "1"
        });

        var response = await client.PostAsync("/ActivityManagement/RemoveTeamMember", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // POST ConfirmBooking - explicit groupId selection redirect location
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ConfirmBooking_RedirectsToUnconfirmedBookings()
    {
        using var factory = new CedevaWebApplicationFactory();
        int bookingId = 0;
        int groupId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var group = TestData.Group(activity, "Les Renards");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            booking.IsConfirmed = false;
            ctx.AddRange(org, activity, group, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            groupId = group.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsync("/ActivityManagement/ConfirmBooking",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["bookingId"] = bookingId.ToString(),
                ["groupId"] = groupId.ToString()
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be("/ActivityManagement/UnconfirmedBookings");
    }

    // ---------------------------------------------------------------------
    // POST AssignToGroup - group not found branch
    // ---------------------------------------------------------------------

    [Fact]
    public async Task AssignToGroup_WithUnknownGroup_ReturnsNotFound()
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
            ctx.AddRange(org, activity, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/AssignToGroup",
            new { BookingId = bookingId, GroupId = 777888 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    // ---------------------------------------------------------------------
    // POST UpdateBooking - group not found, group assignment, and confirm+complete branches
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdateBooking_WithUnknownGroup_ReturnsNotFound()
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
            ctx.AddRange(org, activity, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/UpdateBooking",
            new { BookingId = bookingId, GroupId = 999000 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task UpdateBooking_AssigningRealGroupToCompleteBooking_ReportsComplete()
    {
        using var factory = new CedevaWebApplicationFactory();
        int bookingId = 0;
        int groupId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var group = TestData.Group(activity, "Les Hiboux");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 0m); // confirmed
            booking.IsMedicalSheet = true;
            ctx.AddRange(org, activity, group, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            groupId = group.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/UpdateBooking",
            new { BookingId = bookingId, GroupId = groupId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("isComplete").GetBoolean().Should().BeTrue();

        using var db = factory.NewDbContext();
        var saved = await db.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == bookingId);
        saved.GroupId.Should().Be(groupId);
    }

    [Fact]
    public async Task UpdateBooking_ConfirmingBookingWithoutGroup_CreatesDefaultGroupAndIsNotComplete()
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
            var booking = TestData.Booking(child, activity, null, 100m, 0m);
            booking.IsConfirmed = false;
            booking.IsMedicalSheet = true;
            ctx.AddRange(org, activity, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            activityId = activity.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/UpdateBooking",
            new { BookingId = bookingId, IsConfirmed = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        // Default "Sans groupe" group => booking still not considered complete.
        doc.RootElement.GetProperty("isComplete").GetBoolean().Should().BeFalse();

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
    public async Task UpdateBooking_WithNoFieldsToUpdate_StillSucceeds()
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
            ctx.AddRange(org, activity, parent, child, booking);
            ctx.SaveChanges();
            bookingId = booking.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityManagement/UpdateBooking",
            new { BookingId = bookingId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");
    }

    // ---------------------------------------------------------------------
    // GET GetManageBookingsStats - empty / no-attention buckets
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetManageBookingsStats_WithFullyHandledBooking_ReturnsZeros()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var group = TestData.Group(activity, "Les Castors");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, group, 100m, 0m); // confirmed + real group
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
        root.GetProperty("pendingConfirmation").GetInt32().Should().Be(0);
        root.GetProperty("withoutGroup").GetInt32().Should().Be(0);
        root.GetProperty("withoutMedicalSheet").GetInt32().Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // GET Print - happy path with a reserved booking renders child content
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Print_WithReservedBooking_RendersChildName()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;
        int dayId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var day = Day(activity, "Jeudi", new DateTime(2026, 7, 9));
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent); // FirstName "Chloé"
            var booking = TestData.Booking(child, activity, null, 100m, 0m); // confirmed
            var bookingDay = new BookingDay { ActivityDay = day, Booking = booking, IsReserved = true, IsPresent = false };
            ctx.AddRange(org, activity, day, parent, child, booking, bookingDay);
            ctx.SaveChanges();
            activityId = activity.Id;
            dayId = day.DayId;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Print?activityId={activityId}&dayId={dayId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotBeNullOrEmpty();
        // Child last name "Enfant" is ASCII-safe (the first name "Chloé" is HTML-entity encoded in the view).
        html.Should().Contain("Enfant");
    }
}
