using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Covers the AJAX day-range editor (<c>ActivitiesController.AdjustActivityDays</c>): extend/shrink
/// the range by one day at each end. Shrinking a day with reserved bookings needs confirmation; on
/// confirmation the BookingDays are removed and the booking total is decremented by one PricePerDay
/// (excursion costs in the total are preserved). Antiforgery is bypassed by the test factory.
/// </summary>
[Collection("WebApp")]
public class ActivitiesDayEditTests
{
    private sealed record Seeded(int OrgId, int ActivityId, int BookingId, int FirstDayId, int LastDayId);

    private static Seeded Seed(CedevaWebApplicationFactory factory, bool withBooking)
    {
        Organisation org = null!;
        Activity activity = null!;
        ActivityDay d1 = null!, d2 = null!;
        Booking booking = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org); // PricePerDay = 20
            activity.StartDate = new DateTime(2026, 7, 6);  // Monday
            activity.EndDate = new DateTime(2026, 7, 7);     // Tuesday
            d1 = new ActivityDay { Label = "Lundi 6", DayDate = activity.StartDate, IsActive = true, Week = 1, Activity = activity };
            d2 = new ActivityDay { Label = "Mardi 7", DayDate = activity.EndDate, IsActive = true, Week = 1, Activity = activity };
            activity.Days.Add(d1);
            activity.Days.Add(d2);
            ctx.AddRange(org, activity);
            if (withBooking)
            {
                var parent = TestData.Parent(org);
                var child = TestData.Child(parent);
                // Total = 2 days x 20 (=40) + 15 excursion = 55, to prove the day-removal decrement
                // (–20) preserves the 15 excursion component.
                booking = TestData.Booking(child, activity, group: null, totalAmount: 55m, paidAmount: 0m);
                ctx.AddRange(parent, child, booking);
            }
            return 0;
        });

        if (withBooking)
        {
            factory.Seed(ctx =>
            {
                ctx.BookingDays.AddRange(
                    new BookingDay { BookingId = booking.Id, ActivityDayId = d1.DayId, IsReserved = true },
                    new BookingDay { BookingId = booking.Id, ActivityDayId = d2.DayId, IsReserved = true });
                return 0;
            });
        }

        return new Seeded(org.Id, activity.Id, withBooking ? booking.Id : 0, d1.DayId, d2.DayId);
    }

    private static FormUrlEncodedContent Form(int id, string edge, string op, bool confirmed = false) =>
        new(new Dictionary<string, string>
        {
            ["id"] = id.ToString(),
            ["edge"] = edge,
            ["op"] = op,
            ["confirmed"] = confirmed ? "true" : "false",
        });

    [Fact]
    public async Task Extend_End_AddsDay_AndMovesEndDate()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = Seed(factory, withBooking: false);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Activities/AdjustActivityDays", Form(s.ActivityId, "end", "extend"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var activity = await db.Activities.IgnoreQueryFilters().Include(a => a.Days).FirstAsync(a => a.Id == s.ActivityId);
        activity.EndDate.Should().Be(new DateTime(2026, 7, 8), "the end date moves one day later");
        activity.Days.Should().Contain(d => d.DayDate == new DateTime(2026, 7, 8) && d.IsActive, "the new day is added active");
    }

    [Fact]
    public async Task Shrink_End_NoReservation_DeactivatesEdgeDay()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = Seed(factory, withBooking: false);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Activities/AdjustActivityDays", Form(s.ActivityId, "end", "shrink"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var activity = await db.Activities.IgnoreQueryFilters().Include(a => a.Days).FirstAsync(a => a.Id == s.ActivityId);
        activity.EndDate.Should().Be(new DateTime(2026, 7, 6), "the range tightens to the remaining day");
        activity.Days.First(d => d.DayId == s.LastDayId).IsActive.Should().BeFalse("the edge day is deactivated");
    }

    [Fact]
    public async Task Shrink_End_WithReservation_NeedsConfirmation_ThenRemovesAndDecrementsTotal()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = Seed(factory, withBooking: true);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        // Without confirmation -> the endpoint asks for it and changes nothing.
        var first = await client.PostAsync("/Activities/AdjustActivityDays", Form(s.ActivityId, "end", "shrink", confirmed: false));
        (await first.Content.ReadAsStringAsync()).Should().Contain("\"needsConfirmation\":true");

        using (var db = factory.NewDbContext())
        {
            (await db.BookingDays.IgnoreQueryFilters().CountAsync(bd => bd.ActivityDayId == s.LastDayId)).Should().Be(1);
            (await db.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == s.BookingId)).TotalAmount.Should().Be(55m);
        }

        // With confirmation -> the day's BookingDay is removed and the total drops by one PricePerDay.
        var confirmed = await client.PostAsync("/Activities/AdjustActivityDays", Form(s.ActivityId, "end", "shrink", confirmed: true));
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = factory.NewDbContext();
        (await verify.BookingDays.IgnoreQueryFilters().CountAsync(bd => bd.ActivityDayId == s.LastDayId))
            .Should().Be(0, "the reserved day is removed from bookings");
        (await verify.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == s.BookingId)).TotalAmount
            .Should().Be(35m, "total drops by one PricePerDay (20), preserving the 15 excursion component");
    }

    [Fact]
    public async Task Shrink_LastRemainingDay_IsRejected()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = Seed(factory, withBooking: false);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        // Remove one day (2 -> 1), then attempt to remove the last one.
        await client.PostAsync("/Activities/AdjustActivityDays", Form(s.ActivityId, "end", "shrink"));
        var response = await client.PostAsync("/Activities/AdjustActivityDays", Form(s.ActivityId, "end", "shrink"));

        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");

        using var db = factory.NewDbContext();
        (await db.Activities.IgnoreQueryFilters().Include(a => a.Days).FirstAsync(a => a.Id == s.ActivityId))
            .Days.Count(d => d.IsActive).Should().Be(1, "the last remaining day is kept");
    }
}
