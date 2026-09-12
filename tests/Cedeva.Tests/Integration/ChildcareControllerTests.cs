using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>Coverage for ActivityManagementController's garderie feature (Lot K #5).</summary>
[Collection("WebApp")]
public class ChildcareControllerTests
{
    private sealed record Seeded(int OrgId, int ActivityId, int BookingId, int DayId);

    private static Seeded SeedReservedBooking(CedevaWebApplicationFactory factory, decimal? childcarePricePerDay = 5m)
    {
        Organisation org = null!;
        Activity activity = null!;
        Booking booking = null!;
        ActivityDay day = null!;

        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Garderie");
            activity.ChildcarePricePerDay = childcarePricePerDay;
            day = new ActivityDay { Label = "J1", DayDate = new DateTime(2026, 7, 6), IsActive = true, Activity = activity };
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 0m);
            booking.Days.Add(new BookingDay { ActivityDay = day, IsReserved = true });
            ctx.AddRange(org, activity, day, parent, child, booking);
            return 0;
        });

        return new Seeded(org.Id, activity.Id, booking.Id, day.DayId);
    }

    [Fact]
    public async Task Childcare_Get_ListsReservedChildrenForSelectedDay()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedReservedBooking(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/ActivityManagement/Childcare?id={s.ActivityId}&dayId={s.DayId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ToggleChildcare_Register_CreatesRegistrationAndIncreasesBookingTotal()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedReservedBooking(factory, childcarePricePerDay: 5m);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/ActivityManagement/ToggleChildcare", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["bookingId"] = s.BookingId.ToString(),
                ["activityDayId"] = s.DayId.ToString(),
                ["register"] = "true",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");

        await using var ctx = factory.NewDbContext();
        var registration = await ctx.ChildcareRegistrations.SingleAsync(r => r.BookingId == s.BookingId && r.ActivityDayId == s.DayId);
        registration.Amount.Should().Be(5m);

        var booking = await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.TotalAmount.Should().Be(105m); // 100 + 5
    }

    [Fact]
    public async Task ToggleChildcare_Unregister_RemovesRegistrationAndDecreasesBookingTotal()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedReservedBooking(factory, childcarePricePerDay: 5m);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        (await client.PostAsync("/ActivityManagement/ToggleChildcare", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["bookingId"] = s.BookingId.ToString(),
                ["activityDayId"] = s.DayId.ToString(),
                ["register"] = "true",
            }))).EnsureSuccessStatusCode();

        var response = await client.PostAsync("/ActivityManagement/ToggleChildcare", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["bookingId"] = s.BookingId.ToString(),
                ["activityDayId"] = s.DayId.ToString(),
                ["register"] = "false",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.ChildcareRegistrations.AnyAsync(r => r.BookingId == s.BookingId && r.ActivityDayId == s.DayId)).Should().BeFalse();

        var booking = await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.TotalAmount.Should().Be(100m);
    }

    [Fact]
    public async Task UpdateChildcareAmount_AdjustsBookingTotalByDelta()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedReservedBooking(factory, childcarePricePerDay: 5m);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        (await client.PostAsync("/ActivityManagement/ToggleChildcare", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["bookingId"] = s.BookingId.ToString(),
                ["activityDayId"] = s.DayId.ToString(),
                ["register"] = "true",
            }))).EnsureSuccessStatusCode();

        int registrationId;
        await using (var ctx = factory.NewDbContext())
        {
            registrationId = (await ctx.ChildcareRegistrations.SingleAsync(r => r.BookingId == s.BookingId)).Id;
        }

        var response = await client.PostAsync("/ActivityManagement/UpdateChildcareAmount", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["registrationId"] = registrationId.ToString(),
                ["amount"] = "8",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verify = factory.NewDbContext();
        (await verify.ChildcareRegistrations.SingleAsync(r => r.Id == registrationId)).Amount.Should().Be(8m);
        (await verify.Bookings.SingleAsync(b => b.Id == s.BookingId)).TotalAmount.Should().Be(108m); // 100 + 8
    }

    [Fact]
    public async Task ToggleChildcare_WithoutChildcarePriceConfigured_DefaultsToZero()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedReservedBooking(factory, childcarePricePerDay: null);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        (await client.PostAsync("/ActivityManagement/ToggleChildcare", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["bookingId"] = s.BookingId.ToString(),
                ["activityDayId"] = s.DayId.ToString(),
                ["register"] = "true",
            }))).EnsureSuccessStatusCode();

        await using var ctx = factory.NewDbContext();
        (await ctx.ChildcareRegistrations.SingleAsync(r => r.BookingId == s.BookingId)).Amount.Should().Be(0m);
        (await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId)).TotalAmount.Should().Be(100m);
    }
}
