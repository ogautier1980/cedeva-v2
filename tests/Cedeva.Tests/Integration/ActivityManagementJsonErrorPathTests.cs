using System.Net;
using System.Net.Http.Json;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Drives the defensive catch branches of the ActivityManagement JSON endpoints
/// <c>AssignToGroup</c> and <c>UpdateBooking</c> by forcing persistence to fail
/// (<see cref="ThrowingSaveChangesInterceptor"/>). These deliberately answer with
/// <c>StatusCode(500, new { success = false, ... })</c> — a *handled* JSON error envelope, not an
/// unhandled crash — so we assert the 500 carries that JSON body and the change isn't persisted.
/// </summary>
[Collection("WebApp")]
public class ActivityManagementJsonErrorPathTests
{
    private sealed record Seeded(int OrgId, int BookingId, int GroupId);

    private static (CedevaWebApplicationFactory factory, Seeded seed) Seed()
    {
        var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Booking booking = null!;
        ActivityGroup group = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org);
            group = TestData.Group(activity, "Lions");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 0m);
            booking.IsConfirmed = false;
            ctx.AddRange(org, activity, group, parent, child, booking);
            return 0;
        });
        return (factory, new Seeded(org.Id, booking.Id, group.Id));
    }

    private static async Task AssertHandledJson500(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":false", "the failure must be the handled JSON envelope, not an unhandled crash");
    }

    [Theory]
    [MemberData(nameof(SaveFailures.Kinds), MemberType = typeof(SaveFailures))]
    public async Task AssignToGroup_WhenSaveFails_ReturnsHandledJson500_AndNotAssigned(string kind)
    {
        var (factory, s) = Seed();
        using (factory)
        {
            var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
            factory.ThrowOnSaveChanges = SaveFailures.Make(kind);

            var response = await client.PostAsJsonAsync("/ActivityManagement/AssignToGroup",
                new { BookingId = s.BookingId, GroupId = s.GroupId });

            await AssertHandledJson500(response);

            factory.ThrowOnSaveChanges = null;
            using var db = factory.NewDbContext();
            (await db.Bookings.SingleAsync(b => b.Id == s.BookingId)).GroupId
                .Should().BeNull("a failed assign must not persist the group");
        }
    }

    [Theory]
    [MemberData(nameof(SaveFailures.Kinds), MemberType = typeof(SaveFailures))]
    public async Task UpdateBooking_WhenSaveFails_ReturnsHandledJson500_AndNotUpdated(string kind)
    {
        var (factory, s) = Seed();
        using (factory)
        {
            var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
            factory.ThrowOnSaveChanges = SaveFailures.Make(kind);

            var response = await client.PostAsJsonAsync("/ActivityManagement/UpdateBooking",
                new { BookingId = s.BookingId, IsConfirmed = true });

            await AssertHandledJson500(response);

            factory.ThrowOnSaveChanges = null;
            using var db = factory.NewDbContext();
            (await db.Bookings.SingleAsync(b => b.Id == s.BookingId)).IsConfirmed
                .Should().BeFalse("a failed update must not persist the confirmation");
        }
    }
}
