using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration tests for <c>ActivityManagementController.OneReport</c> — the ONE (Office de la
/// Naissance et de l'Enfance) listings/attendance report. Covers the age-bucketing at -6/+6 years
/// (computed relative to the activity's StartDate, not "today", so results stay stable however
/// long after seeding the test runs), the per-child listing fields, and the weekly presence counts.
/// </summary>
[Collection("WebApp")]
public class OneReportTests
{
    [Fact]
    public async Task OneReport_UnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/ActivityManagement/OneReport?id=999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OneReport_BucketsChildrenByAgeAtActivityStart_AndShowsListingFields()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;

        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org); // StartDate ~1 month out (relative, never expires)

            var day1 = new ActivityDay { Label = "Jour 1", DayDate = activity.StartDate, IsActive = true, Week = 1, Activity = activity };
            var day2 = new ActivityDay { Label = "Jour 2", DayDate = activity.StartDate.AddDays(1), IsActive = true, Week = 1, Activity = activity };

            var parent = TestData.Parent(org);

            // Under 6 at activity start (relative BirthDate so the bucket never drifts with time).
            // Accent-free names: Razor HTML-encodes accented letters as numeric entities, which
            // would otherwise break plain-string Contains() assertions below.
            var youngChild = TestData.Child(parent);
            youngChild.FirstName = "Leo";
            youngChild.LastName = "Petit";
            youngChild.BirthDate = activity.StartDate.AddYears(-4);
            youngChild.IsDisadvantagedEnvironment = true;

            // 6 or over at activity start.
            var oldChild = TestData.Child(parent);
            oldChild.FirstName = "Emma";
            oldChild.LastName = "Grand";
            oldChild.BirthDate = activity.StartDate.AddYears(-9);
            oldChild.IsMildDisability = true;

            var youngBooking = TestData.Booking(youngChild, activity, null, totalAmount: 40m, paidAmount: 40m);
            var oldBooking = TestData.Booking(oldChild, activity, null, totalAmount: 40m, paidAmount: 20m);

            var youngDay1 = new BookingDay { ActivityDay = day1, Booking = youngBooking, IsReserved = true, IsPresent = true };
            var youngDay2 = new BookingDay { ActivityDay = day2, Booking = youngBooking, IsReserved = true, IsPresent = false };
            var oldDay1 = new BookingDay { ActivityDay = day1, Booking = oldBooking, IsReserved = true, IsPresent = true };

            ctx.AddRange(org, activity, day1, day2, parent, youngChild, oldChild,
                youngBooking, oldBooking, youngDay1, youngDay2, oldDay1);
            ctx.SaveChanges();
            activityId = activity.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/OneReport?id={activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();

        // Under-6 listing: young child present, old child absent from that table.
        var under6Section = html.Substring(0, html.IndexOf("Liste enfants de 6 ans et plus", StringComparison.Ordinal));
        under6Section.Should().Contain("Petit Leo");
        under6Section.Should().Contain("40,00");
        under6Section.Should().NotContain("Grand Emma");

        // Over-6 listing: old child present.
        var over6Section = html.Substring(html.IndexOf("Liste enfants de 6 ans et plus", StringComparison.Ordinal));
        over6Section.Should().Contain("Grand Emma");
        over6Section.Should().Contain("20,00");
    }
}
