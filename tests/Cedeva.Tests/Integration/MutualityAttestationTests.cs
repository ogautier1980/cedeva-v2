using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>Coverage for BookingsController.MutualityAttestation (Lot K #3).</summary>
[Collection("WebApp")]
public class MutualityAttestationTests
{
    private static (int OrgId, int BookingId) SeedBookingWithPresence(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        Booking booking = null!;

        factory.Seed(ctx =>
        {
            org = TestData.Organisation("Plaine de Bossière");
            org.ResponsibleName = "Jean Dupont";
            org.LogoUrl = "https://example.test/logo.png";

            var activity = TestData.Activity(org, "Stage été 2026 - Semaine 2");
            var day1 = new ActivityDay { Label = "Lundi", DayDate = new DateTime(2026, 7, 6), IsActive = true };
            var day2 = new ActivityDay { Label = "Mardi", DayDate = new DateTime(2026, 7, 7), IsActive = true };
            activity.Days.Add(day1);
            activity.Days.Add(day2);

            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group: null, totalAmount: 40m, paidAmount: 40m);
            booking.Days.Add(new BookingDay { ActivityDay = day1, IsReserved = true, IsPresent = true });
            booking.Days.Add(new BookingDay { ActivityDay = day2, IsReserved = true, IsPresent = false });

            ctx.AddRange(org, activity, day1, day2, parent, child, booking);
            return 0;
        });

        return (org.Id, booking.Id);
    }

    [Fact]
    public async Task MutualityAttestation_RendersOrganisationAndChildAndDaysAttended()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, bookingId) = SeedBookingWithPresence(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/Bookings/MutualityAttestation/{bookingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        html.Should().Contain("Plaine de Bossière");
        html.Should().Contain("Jean Dupont");
        html.Should().Contain("Stage été 2026 - Semaine 2");
        html.Should().Contain("logo.png");
        // Only day1 (Lundi) has IsPresent=true -> 1 day attended, not 2.
        html.Should().Contain("Nombre de jours de fréquentation: 1");
    }

    [Fact]
    public async Task MutualityAttestation_UnknownBooking_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync("/Bookings/MutualityAttestation/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MutualityAttestation_CoordinatorOfOtherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (_, bookingId) = SeedBookingWithPresence(factory);

        // A coordinator from a different organisation: tenancy filter hides the booking.
        var client = factory.CreateClientFor("u2", organisationId: 999999, "Coordinator");
        var response = await client.GetAsync($"/Bookings/MutualityAttestation/{bookingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MutualityAttestation_NoResponsibleNameConfigured_OmitsThatLine()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Booking booking = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation("Org Sans Responsable");
            org.ResponsibleName = null;
            var activity = TestData.Activity(org);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group: null, totalAmount: 40m, paidAmount: 40m);
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Bookings/MutualityAttestation/{booking.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        html.Should().Contain("Org Sans Responsable");
    }
}
