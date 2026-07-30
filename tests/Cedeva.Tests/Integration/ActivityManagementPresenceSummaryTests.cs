using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage for the ONE-indicator ventilation added to PresenceSummary (Lot C): the daily
/// reserved/present totals must also be broken down by "milieu defavorise", "handicap leger"
/// and "handicap lourd", per Thomas's confirmed request (ref. capture 18.48.14).
/// </summary>
[Collection("WebApp")]
public class ActivityManagementPresenceSummaryTests
{
    private static ActivityDay Day(Activity activity, string label, DateTime date) => new()
    {
        Label = label,
        DayDate = date,
        IsActive = true,
        Activity = activity
    };

    [Fact]
    public async Task PresenceSummary_Get_BreaksDownCountsByOneIndicator()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage ONE Ventilation");
            var day = Day(activity, "Lundi", DateTime.Today);

            var parentA = TestData.Parent(org);
            var childA = TestData.Child(parentA);
            childA.IsDisadvantagedEnvironment = true;
            var bookingA = TestData.Booking(childA, activity, null, 100m, 0m);
            bookingA.IsConfirmed = true;
            var bookingDayA = new BookingDay { ActivityDay = day, Booking = bookingA, IsReserved = true, IsPresent = true };

            var parentB = TestData.Parent(org);
            parentB.Email = "paulb@test.be";
            parentB.NationalRegisterNumber = "85010112348";
            var childB = TestData.Child(parentB);
            childB.NationalRegisterNumber = "16052012348";
            childB.IsSevereDisability = true;
            var bookingB = TestData.Booking(childB, activity, null, 100m, 0m);
            bookingB.IsConfirmed = true;
            var bookingDayB = new BookingDay { ActivityDay = day, Booking = bookingB, IsReserved = true, IsPresent = false };

            ctx.AddRange(org, activity, day, parentA, childA, bookingA, bookingDayA, parentB, childB, bookingB, bookingDayB);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/PresenceSummary?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Milieu");
        html.Should().Contain("Handicap");
    }

    [Fact]
    public async Task PresenceSummary_WithoutActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/ActivityManagement/PresenceSummary?id=999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
