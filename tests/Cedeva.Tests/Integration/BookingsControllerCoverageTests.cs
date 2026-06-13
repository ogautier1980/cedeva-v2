using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Additional coverage for <c>BookingsController</c>, exercising the actions/branches the
/// existing <c>BookingsControllerIntegrationTests</c> does not touch: Index (default + filters +
/// filter-redirect), Details, Create GET/POST, Edit GET/POST (incl. confirmation-email branch),
/// Delete GET/POST, GetActivityQuestions, Export (Excel) and ExportPdf, plus tenant isolation
/// and not-found edges.
/// </summary>
[Collection("WebApp")]
public class BookingsControllerCoverageTests
{
    private const string Coordinator = "Coordinator";

    /// <summary>Seeds an organisation with one activity (2 active days), a group, a parent, a child
    /// and one confirmed booking with one booking day. Returns the live entity graph for assertions.</summary>
    private sealed record SeedGraph(
        Organisation Org, Activity Activity, ActivityGroup Group,
        Parent Parent, Child Child, Booking Booking,
        ActivityDay Day1, ActivityDay Day2);

    private static SeedGraph SeedFullGraph(CedevaWebApplicationFactory factory, bool bookingConfirmed = true)
    {
        return factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var day1 = new ActivityDay { Label = "Lundi", DayDate = new DateTime(2026, 7, 6), Week = 1, IsActive = true };
            var day2 = new ActivityDay { Label = "Mardi", DayDate = new DateTime(2026, 7, 7), Week = 1, IsActive = true };
            activity.Days.Add(day1);
            activity.Days.Add(day2);
            var group = TestData.Group(activity, "Groupe A");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, group, totalAmount: 40m, paidAmount: 0m);
            booking.IsConfirmed = bookingConfirmed;

            ctx.AddRange(org, activity, day1, day2, group, parent, child, booking);
            ctx.SaveChanges();

            booking.Days.Add(new BookingDay
            {
                BookingId = booking.Id,
                ActivityDayId = day1.DayId,
                IsReserved = true,
                IsPresent = false
            });

            return new SeedGraph(org, activity, group, parent, child, booking, day1, day2);
        });
    }

    // ---------------------------------------------------------------- Index

    [Fact]
    public async Task Index_NoQuery_AsSeededOrgCoordinator_RendersBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync("/Bookings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // Assert on ASCII-only values (the child first name "Chloé" is HTML-encoded in the markup).
        html.Should().Contain(g.Child.LastName);
        html.Should().Contain(g.Activity.Name);
    }

    [Fact]
    public async Task Index_WithQueryParams_RedirectsToIndexToPersistFilters()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync($"/Bookings?searchString={g.Child.FirstName}&sortBy=childname&sortOrder=asc&isConfirmed=true");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        // RedirectToAction(nameof(Index)) -> "/Bookings" (no "Index" segment)
        response.Headers.Location!.ToString().Should().Contain("/Bookings");
        response.Headers.Location!.ToString().Should().NotContain("searchString");
    }

    [Fact]
    public async Task Index_WithSearchString_FollowedThrough_ReturnsMatchingBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        // First request stores filters in session and 302-redirects; follow it manually so the
        // session cookie + KeepFilters TempData carry over and the filtered list renders.
        var redirect = await client.GetAsync($"/Bookings?searchString={g.Child.FirstName}");
        redirect.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var followUp = await client.GetAsync(redirect.Headers.Location!.ToString());
        followUp.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await followUp.Content.ReadAsStringAsync();
        html.Should().Contain(g.Child.LastName);
    }

    [Fact]
    public async Task Index_AsDifferentOrgCoordinator_DoesNotShowOtherOrgBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        // Coordinator of a different organisation (id guaranteed not to match seeded org).
        var client = factory.CreateClientFor("intruder", g.Org.Id + 999, Coordinator);

        var response = await client.GetAsync("/Bookings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // The seeded child's unique full name must not appear for a foreign tenant.
        html.Should().NotContain($"{g.Child.FirstName} {g.Child.LastName}");
    }

    // ---------------------------------------------------------------- Details

    [Fact]
    public async Task Details_ExistingBooking_AsSeededOrgCoordinator_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync($"/Bookings/Details/{g.Booking.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain(g.Child.LastName);
    }

    [Fact]
    public async Task Details_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync("/Bookings/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_AsDifferentOrgCoordinator_StillLoadsBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("intruder", g.Org.Id + 999, Coordinator);

        var response = await client.GetAsync($"/Bookings/Details/{g.Booking.Id}");

        // Documents the ACTUAL behaviour: Details is NOT tenant-isolated. The Booking entity has no
        // multi-tenancy query filter (only Activity/Parent/Child/TeamMember/EmailTemplate do), and
        // GetBookingViewModelAsync resolves the related Child/Parent/Activity via FindAsync, which
        // bypasses global query filters. So a foreign-org coordinator gets 200 and sees the details.
        // (Index, by contrast, IS isolated because it explicitly filters Activity.OrganisationId.)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain(g.Child.LastName);
    }

    // ---------------------------------------------------------------- Create GET

    [Fact]
    public async Task CreateGet_ReturnsForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync("/Bookings/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Create");
    }

    [Fact]
    public async Task CreateGet_WithPreselectedChildAndActivity_ReturnsForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync($"/Bookings/Create?childId={g.Child.Id}&activityId={g.Activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- Create POST

    [Fact]
    public async Task CreatePost_ValidWithSelectedDays_PersistsAndRedirectsToDetails()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("BookingDate", "2026-06-10"),
            new KeyValuePair<string, string>("ChildId", g.Child.Id.ToString()),
            new KeyValuePair<string, string>("ActivityId", g.Activity.Id.ToString()),
            new KeyValuePair<string, string>("IsConfirmed", "false"),
            new KeyValuePair<string, string>("IsMedicalSheet", "false"),
            new KeyValuePair<string, string>("SelectedActivityDayIds", g.Day1.DayId.ToString()),
            new KeyValuePair<string, string>("SelectedActivityDayIds", g.Day2.DayId.ToString()),
        });

        var response = await client.PostAsync("/Bookings/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Bookings/Details");

        await using var db = factory.NewDbContext();
        // PricePerDay (20) * 2 selected days = 40 ; only one booking was seeded (id g.Booking.Id).
        var created = await db.Bookings
            .IgnoreQueryFilters()
            .Include(b => b.Days)
            .Where(b => b.Id != g.Booking.Id && b.ChildId == g.Child.Id)
            .FirstOrDefaultAsync();
        created.Should().NotBeNull();
        created!.TotalAmount.Should().Be(40m);
        created.Days.Should().HaveCount(2);
        created.PaymentStatus.Should().Be(PaymentStatus.NotPaid);
    }

    [Fact]
    public async Task CreatePost_InvalidModel_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        // An unparseable BookingDate produces a model-binding conversion error -> ModelState invalid.
        // (Omitting the required int Child/Activity ids would NOT invalidate ModelState: [Required]
        // on a non-nullable int is satisfied by the default 0, so that path reaches the DB instead.)
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("BookingDate", "not-a-date"),
            new KeyValuePair<string, string>("ChildId", g.Child.Id.ToString()),
            new KeyValuePair<string, string>("ActivityId", g.Activity.Id.ToString()),
            new KeyValuePair<string, string>("IsConfirmed", "false"),
            new KeyValuePair<string, string>("IsMedicalSheet", "false"),
        });

        var response = await client.PostAsync("/Bookings/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-render, not a redirect

        await using var db = factory.NewDbContext();
        var count = await db.Bookings.IgnoreQueryFilters().CountAsync();
        count.Should().Be(1); // still only the seeded booking
    }

    [Fact]
    public async Task CreatePost_UnknownActivity_AddsModelErrorAndReRenders()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("BookingDate", "2026-06-10"),
            new KeyValuePair<string, string>("ChildId", g.Child.Id.ToString()),
            new KeyValuePair<string, string>("ActivityId", "888888"), // valid model, missing activity row
            new KeyValuePair<string, string>("IsConfirmed", "false"),
            new KeyValuePair<string, string>("IsMedicalSheet", "false"),
        });

        var response = await client.PostAsync("/Bookings/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-render, not a redirect

        await using var db = factory.NewDbContext();
        var count = await db.Bookings.IgnoreQueryFilters().CountAsync();
        count.Should().Be(1);
    }

    // ---------------------------------------------------------------- Edit GET

    [Fact]
    public async Task EditGet_ExistingBooking_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync($"/Bookings/Edit/{g.Booking.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EditGet_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync("/Bookings/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- Edit POST

    [Fact]
    public async Task EditPost_ValidNoConfirmationChange_UpdatesAndRedirectsToDetails()
    {
        using var factory = new CedevaWebApplicationFactory();
        // Booking already confirmed -> wasNotConfirmed is false -> no email branch, plain update.
        var g = SeedFullGraph(factory, bookingConfirmed: true);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", g.Booking.Id.ToString()),
            new KeyValuePair<string, string>("BookingDate", "2026-06-12"),
            new KeyValuePair<string, string>("ChildId", g.Child.Id.ToString()),
            new KeyValuePair<string, string>("ActivityId", g.Activity.Id.ToString()),
            new KeyValuePair<string, string>("IsConfirmed", "true"),
            new KeyValuePair<string, string>("IsMedicalSheet", "true"),
            new KeyValuePair<string, string>("SelectedActivityDayIds", g.Day2.DayId.ToString()),
        });

        var response = await client.PostAsync($"/Bookings/Edit/{g.Booking.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Bookings/Details");

        await using var db = factory.NewDbContext();
        var updated = await db.Bookings
            .IgnoreQueryFilters()
            .Include(b => b.Days)
            .FirstAsync(b => b.Id == g.Booking.Id);
        updated.IsMedicalSheet.Should().BeTrue();
        updated.BookingDate.Should().Be(new DateTime(2026, 6, 12));
        // Day1 (seeded) removed, Day2 added by UpdateBookingDays.
        updated.Days.Select(d => d.ActivityDayId).Should().BeEquivalentTo(new[] { g.Day2.DayId });
    }

    [Fact]
    public async Task EditPost_ConfirmingPreviouslyUnconfirmed_HitsEmailBranchAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        // Seed an UNCONFIRMED booking so toggling IsConfirmed exercises SendBookingConfirmationEmailAsync.
        var g = SeedFullGraph(factory, bookingConfirmed: false);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", g.Booking.Id.ToString()),
            new KeyValuePair<string, string>("BookingDate", "2026-06-12"),
            new KeyValuePair<string, string>("ChildId", g.Child.Id.ToString()),
            new KeyValuePair<string, string>("ActivityId", g.Activity.Id.ToString()),
            new KeyValuePair<string, string>("IsConfirmed", "true"),
            new KeyValuePair<string, string>("IsMedicalSheet", "false"),
        });

        var response = await client.PostAsync($"/Bookings/Edit/{g.Booking.Id}", form);

        // Email send is wrapped in try/catch; either success or warning still redirects to Details.
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Bookings/Details");

        await using var db = factory.NewDbContext();
        var updated = await db.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == g.Booking.Id);
        updated.IsConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task EditPost_InvalidModel_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        // Unparseable BookingDate -> model-binding error -> ModelState invalid -> re-render.
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", g.Booking.Id.ToString()),
            new KeyValuePair<string, string>("BookingDate", "not-a-date"),
            new KeyValuePair<string, string>("ChildId", g.Child.Id.ToString()),
            new KeyValuePair<string, string>("ActivityId", g.Activity.Id.ToString()),
            new KeyValuePair<string, string>("IsConfirmed", "true"),
            new KeyValuePair<string, string>("IsMedicalSheet", "false"),
        });

        var response = await client.PostAsync($"/Bookings/Edit/{g.Booking.Id}", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EditPost_UnknownId_ValidModel_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        const int missingId = 777777;
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", missingId.ToString()),
            new KeyValuePair<string, string>("BookingDate", "2026-06-12"),
            new KeyValuePair<string, string>("ChildId", g.Child.Id.ToString()),
            new KeyValuePair<string, string>("ActivityId", g.Activity.Id.ToString()),
            new KeyValuePair<string, string>("IsConfirmed", "true"),
            new KeyValuePair<string, string>("IsMedicalSheet", "false"),
        });

        var response = await client.PostAsync($"/Bookings/Edit/{missingId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- Delete GET

    [Fact]
    public async Task DeleteGet_ExistingBooking_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync($"/Bookings/Delete/{g.Booking.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteGet_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync("/Bookings/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- Delete POST

    [Fact]
    public async Task DeletePost_ExistingBooking_RemovesAndRedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.PostAsync($"/Bookings/Delete/{g.Booking.Id}", new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Bookings");

        await using var db = factory.NewDbContext();
        var exists = await db.Bookings.IgnoreQueryFilters().AnyAsync(b => b.Id == g.Booking.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeletePost_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.PostAsync("/Bookings/Delete/999999", new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- GetActivityQuestions (JSON)

    [Fact]
    public async Task GetActivityQuestions_ReturnsOnlyActiveQuestionsAsJson()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            var qActive = TestData.Question(a, "QuestionActive", isActive: true, displayOrder: 1);
            var qInactive = TestData.Question(a, "QuestionInactive", isActive: false, displayOrder: 2);
            ctx.AddRange(org, a, qActive, qInactive);
            return a;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: Coordinator);
        var response = await client.GetAsync($"/Bookings/GetActivityQuestions?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("QuestionActive");
        json.Should().NotContain("QuestionInactive");
    }

    [Fact]
    public async Task GetActivityQuestions_UnknownActivity_ReturnsEmptyQuestionsJson()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", organisationId: null, role: Coordinator);

        var response = await client.GetAsync("/Bookings/GetActivityQuestions?activityId=424242");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("questions");
    }

    // ---------------------------------------------------------------- Export
    // The export actions materialize the in-memory repository result synchronously (ToList),
    // so they return a real file.

    [Fact]
    public async Task Export_ReturnsExcelFile()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync("/Bookings/Export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await response.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_WithFilters_ReturnsExcelFile()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync(
            $"/Bookings/Export?searchString=Enfant&activityId={g.Activity.Id}&childId={g.Child.Id}&isConfirmed=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
    }

    // ---------------------------------------------------------------- ExportPdf

    [Fact]
    public async Task ExportPdf_ReturnsPdfFile()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.Org.Id, Coordinator);

        var response = await client.GetAsync("/Bookings/ExportPdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
    }

    // ---------------------------------------------------------------- Auth

    [Fact]
    public async Task Index_Unauthenticated_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Bookings");

        // No cookie auth in tests -> challenge yields 401 (or a redirect to the login path).
        var isChallenge = response.StatusCode == HttpStatusCode.Unauthorized
            || (response.StatusCode == HttpStatusCode.Redirect
                && response.Headers.Location!.ToString().Contains("/Account/Login"));
        isChallenge.Should().BeTrue();
    }
}
