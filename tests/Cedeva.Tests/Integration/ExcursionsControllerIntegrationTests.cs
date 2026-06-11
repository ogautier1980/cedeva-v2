using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class ExcursionsControllerIntegrationTests
{
    private sealed record Seeded(int OrgId, int ExcursionId, int BookingId);

    private static Seeded SeedExcursionScenario(CedevaWebApplicationFactory factory, decimal cost = 15m)
    {
        Excursion excursion = null!;
        Booking booking = null!;
        Organisation org = null!;

        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var group = TestData.Group(activity, "Lions");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group, totalAmount: 100m, paidAmount: 0m);
            excursion = TestData.Excursion(activity, cost);
            var link = TestData.ExcursionGroup(excursion, group);
            ctx.AddRange(org, activity, group, parent, child, booking, excursion, link);
            return 0;
        });

        return new Seeded(org.Id, excursion.Id, booking.Id);
    }

    [Fact]
    public async Task RegisterChild_ValidPost_RegistersAndIncreasesBookingTotal()
    {
        using var factory = new CedevaWebApplicationFactory();
        var seeded = SeedExcursionScenario(factory, cost: 15m);
        var client = factory.CreateClientFor("u1", seeded.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/RegisterChild", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["excursionId"] = seeded.ExcursionId.ToString(),
                ["bookingId"] = seeded.BookingId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");

        await using var ctx = factory.NewDbContext();
        var booking = await ctx.Bookings.SingleAsync(b => b.Id == seeded.BookingId);
        booking.TotalAmount.Should().Be(115m); // 100 + 15
        (await ctx.ExcursionRegistrations.AnyAsync(r => r.BookingId == seeded.BookingId)).Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterChild_AfterRegister_RevertsBookingTotal()
    {
        using var factory = new CedevaWebApplicationFactory();
        var seeded = SeedExcursionScenario(factory, cost: 15m);
        var client = factory.CreateClientFor("u1", seeded.OrgId, "Coordinator");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["excursionId"] = seeded.ExcursionId.ToString(),
            ["bookingId"] = seeded.BookingId.ToString(),
        });

        (await client.PostAsync("/Excursions/RegisterChild", form)).EnsureSuccessStatusCode();

        var response = await client.PostAsync("/Excursions/UnregisterChild", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["excursionId"] = seeded.ExcursionId.ToString(),
                ["bookingId"] = seeded.BookingId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");

        await using var ctx = factory.NewDbContext();
        var booking = await ctx.Bookings.SingleAsync(b => b.Id == seeded.BookingId);
        booking.TotalAmount.Should().Be(100m); // 115 after register, back to 100
        (await ctx.ExcursionRegistrations.AnyAsync(r => r.BookingId == seeded.BookingId)).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAttendance_MarksRegistrationPresent()
    {
        using var factory = new CedevaWebApplicationFactory();
        var seeded = SeedExcursionScenario(factory);
        var client = factory.CreateClientFor("u1", seeded.OrgId, "Coordinator");

        (await client.PostAsync("/Excursions/RegisterChild", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["excursionId"] = seeded.ExcursionId.ToString(),
                ["bookingId"] = seeded.BookingId.ToString(),
            }))).EnsureSuccessStatusCode();

        int registrationId;
        await using (var ctx = factory.NewDbContext())
        {
            registrationId = (await ctx.ExcursionRegistrations.SingleAsync(r => r.BookingId == seeded.BookingId)).Id;
        }

        var response = await client.PostAsync("/Excursions/UpdateAttendance", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["registrationId"] = registrationId.ToString(),
                ["isPresent"] = "true",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");

        await using var verify = factory.NewDbContext();
        (await verify.ExcursionRegistrations.SingleAsync(r => r.Id == registrationId)).IsPresent.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterChild_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        var seeded = SeedExcursionScenario(factory);
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/Excursions/RegisterChild", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["excursionId"] = seeded.ExcursionId.ToString(),
                ["bookingId"] = seeded.BookingId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
