using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration tests for the anonymous public registration flow
/// (<see cref="Cedeva.Website.Features.PublicRegistration.PublicRegistrationController"/>).
/// These endpoints are decorated with [AllowAnonymous] and must work WITHOUT a logged-in user.
/// </summary>
[Collection("WebApp")]
public class PublicRegistrationControllerIntegrationTests
{
    /// <summary>Client with no auth header — exercises the genuine anonymous path.</summary>
    private static HttpClient Anonymous(CedevaWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // ----- GET Register -----

    [Fact]
    public async Task Register_FutureActivity_AnonymousUser_ReturnsOkWithActivityName()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org, "Stage Public Ete"); // StartDate 2026-07-01 (future)
            ctx.AddRange(org, a);
            return a;
        });

        var client = Anonymous(factory);
        var response = await client.GetAsync($"/PublicRegistration/Register?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stage Public Ete");
    }

    [Fact]
    public async Task Register_UnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0); // schema only

        var client = Anonymous(factory);
        var response = await client.GetAsync("/PublicRegistration/Register?activityId=999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_PastActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            // Move into the past so the controller's "StartDate > DateTime.Now" filter excludes it.
            a.StartDate = new DateTime(2000, 1, 1);
            a.EndDate = new DateTime(2000, 1, 5);
            ctx.AddRange(org, a);
            return a;
        });

        var client = Anonymous(factory);
        var response = await client.GetAsync($"/PublicRegistration/Register?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----- GET SelectActivity -----

    [Fact]
    public async Task SelectActivity_AnonymousUser_ListsOnlyFutureActivitiesForOrganisation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var org = factory.Seed(ctx =>
        {
            var o = TestData.Organisation();
            var future = TestData.Activity(o, "Stage Futur");        // 2026-07-01
            var past = TestData.Activity(o, "Stage Passe");
            past.StartDate = new DateTime(2000, 1, 1);
            past.EndDate = new DateTime(2000, 1, 5);
            ctx.AddRange(o, future, past);
            return o;
        });

        var client = Anonymous(factory);
        var response = await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stage Futur");
        html.Should().NotContain("Stage Passe");
    }

    [Fact]
    public async Task SelectActivity_AnonymousUser_OtherOrganisationActivitiesAreNotListed()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgA = factory.Seed(ctx =>
        {
            var a = TestData.Organisation("Org A");
            var b = TestData.Organisation("Org B");
            var activityA = TestData.Activity(a, "Stage Org A");
            var activityB = TestData.Activity(b, "Stage Org B");
            ctx.AddRange(a, b, activityA, activityB);
            return a;
        });

        var client = Anonymous(factory);
        var response = await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={orgA.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stage Org A");
        html.Should().NotContain("Stage Org B");
    }

    [Fact]
    public async Task SelectActivity_UnknownOrganisation_ReturnsOkWithNoActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var o = TestData.Organisation();
            var a = TestData.Activity(o, "Stage Existant");
            ctx.AddRange(o, a);
            return 0;
        });

        var client = Anonymous(factory);
        var response = await client.GetAsync("/PublicRegistration/SelectActivity?orgId=999999");

        // The action always returns the view; the unknown org simply yields an empty list.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("Stage Existant");
    }

    // ----- GET Confirmation -----

    [Fact]
    public async Task Confirmation_ExistingBooking_AnonymousUser_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var booking = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var activity = TestData.Activity(org, "Stage Confirme");
            var b = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 0m);
            ctx.AddRange(org, parent, child, activity, b);
            return b;
        });

        var client = Anonymous(factory);
        var response = await client.GetAsync($"/PublicRegistration/Confirmation?bookingId={booking.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Confirmation_UnknownBooking_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = Anonymous(factory);
        var response = await client.GetAsync("/PublicRegistration/Confirmation?bookingId=999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----- POST Register (happy path persists booking) -----

    [Fact]
    public async Task Register_Post_ValidData_AnonymousUser_CreatesBookingAndRedirectsToConfirmation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org, "Stage Inscription");
            ctx.AddRange(org, a);
            return a;
        });

        var client = Anonymous(factory);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = activity.Id.ToString(),
            ["ParentFirstName"] = "Paul",
            ["ParentLastName"] = "Parent",
            ["ParentEmail"] = "paul.public@test.be",
            ["ParentPhoneNumber"] = "021234567",
            ["ParentNationalRegisterNumber"] = "85.06.15-133.80",
            ["ParentStreet"] = "Rue Publique 1",
            ["ParentPostalCode"] = "1000",
            ["ParentCity"] = "Bruxelles",
            ["ChildFirstName"] = "Enzo",
            ["ChildLastName"] = "Enfant",
            ["ChildBirthDate"] = "2016-07-08",
            ["ChildNationalRegisterNumber"] = "16.07.08-164.10",
        });

        var response = await client.PostAsync("/PublicRegistration/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found); // 302 -> Confirmation
        response.Headers.Location!.ToString().Should().Contain("Confirmation");

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters()
            .Any(b => b.ActivityId == activity.Id)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Register_Post_MissingRequiredFields_ReturnsOkAndCreatesNoBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org, "Stage Validation");
            ctx.AddRange(org, a);
            return a;
        });

        var client = Anonymous(factory);
        // Only the ActivityId is supplied — all required parent/child fields are missing,
        // so ModelState is invalid and the view is re-rendered (200) with no booking created.
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = activity.Id.ToString(),
        });

        var response = await client.PostAsync("/PublicRegistration/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters()
            .Any(b => b.ActivityId == activity.Id)
            .Should().BeFalse();
    }

    // ----- EmbedCode requires authorisation -----

    [Fact]
    public async Task EmbedCode_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = Anonymous(factory);
        var response = await client.GetAsync("/PublicRegistration/EmbedCode/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmbedCode_AuthorisedCoordinator_ExistingActivity_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Embed");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/PublicRegistration/EmbedCode/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
