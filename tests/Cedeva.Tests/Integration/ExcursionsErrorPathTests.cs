using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Drives the defensive catch branches of the Excursions JSON endpoints (RegisterChild /
/// UnregisterChild / UpdateAttendance) by forcing persistence to fail
/// (<see cref="ThrowingSaveChangesInterceptor"/>). Each wraps SaveChanges in
/// catch(InvalidOperationException)/catch(DbUpdateException)/catch(Exception) and returns a JSON
/// error (HTTP 200, never 500); the side effect must not happen.
/// </summary>
[Collection("WebApp")]
public class ExcursionsErrorPathTests
{
    private sealed record Seeded(int OrgId, int ExcursionId, int BookingId);

    private static Seeded Seed(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        Excursion excursion = null!;
        Booking booking = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var group = TestData.Group(activity, "Lions");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group, totalAmount: 100m, paidAmount: 0m);
            excursion = TestData.Excursion(activity, cost: 15m);
            var link = TestData.ExcursionGroup(excursion, group);
            ctx.AddRange(org, activity, group, parent, child, booking, excursion, link);
            return 0;
        });
        return new Seeded(org.Id, excursion.Id, booking.Id);
    }

    private static int RegisterChild(CedevaWebApplicationFactory factory, HttpClient client, Seeded s)
    {
        var resp = client.PostAsync("/Excursions/RegisterChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["bookingId"] = s.BookingId.ToString(),
        })).GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
        using var ctx = factory.NewDbContext();
        return ctx.ExcursionRegistrations.Single(r => r.BookingId == s.BookingId).Id;
    }

    [Theory]
    [MemberData(nameof(SaveFailures.Kinds), MemberType = typeof(SaveFailures))]
    public async Task RegisterChild_WhenSaveFails_ReturnsJsonError_AndNotRegistered(string kind)
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = Seed(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
        factory.ThrowOnSaveChanges = SaveFailures.Make(kind);

        var response = await client.PostAsync("/Excursions/RegisterChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["bookingId"] = s.BookingId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the JSON endpoint catches the failure, never 500");

        factory.ThrowOnSaveChanges = null;
        using var db = factory.NewDbContext();
        (await db.ExcursionRegistrations.AnyAsync(r => r.BookingId == s.BookingId))
            .Should().BeFalse("a failed registration must not persist");
    }

    [Theory]
    [MemberData(nameof(SaveFailures.Kinds), MemberType = typeof(SaveFailures))]
    public async Task UnregisterChild_WhenSaveFails_ReturnsJsonError_AndStillRegistered(string kind)
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = Seed(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
        RegisterChild(factory, client, s); // create the registration before injecting the fault

        factory.ThrowOnSaveChanges = SaveFailures.Make(kind);
        var response = await client.PostAsync("/Excursions/UnregisterChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["excursionId"] = s.ExcursionId.ToString(),
            ["bookingId"] = s.BookingId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        factory.ThrowOnSaveChanges = null;
        using var db = factory.NewDbContext();
        (await db.ExcursionRegistrations.AnyAsync(r => r.BookingId == s.BookingId))
            .Should().BeTrue("a failed unregister must leave the registration in place");
    }

    [Theory]
    [MemberData(nameof(SaveFailures.Kinds), MemberType = typeof(SaveFailures))]
    public async Task UpdateAttendance_WhenSaveFails_ReturnsJsonError_AndUnchanged(string kind)
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = Seed(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
        var registrationId = RegisterChild(factory, client, s);

        factory.ThrowOnSaveChanges = SaveFailures.Make(kind);
        var response = await client.PostAsync("/Excursions/UpdateAttendance", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["registrationId"] = registrationId.ToString(),
            ["isPresent"] = "true",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        factory.ThrowOnSaveChanges = null;
        using var db = factory.NewDbContext();
        (await db.ExcursionRegistrations.SingleAsync(r => r.Id == registrationId)).IsPresent
            .Should().BeFalse("a failed attendance update must not persist");
    }
}
