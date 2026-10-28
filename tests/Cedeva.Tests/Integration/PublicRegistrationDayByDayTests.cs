using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Helpers;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Public registration for an activity with <see cref="Activity.AllowDayByDayRegistration"/>: the
/// parent ticks individual days instead of whole weeks, and only those days are booked and charged.
/// </summary>
[Collection("WebApp")]
public class PublicRegistrationDayByDayTests
{
    private static HttpClient Anonymous(CedevaWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static DateTime NextMonday()
    {
        var date = DateTime.Today.AddMonths(1);
        while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(1);
        return date;
    }

    /// <summary>Two-week activity (Mon-Fri x2), 20€/day, returns its day ids in date order.</summary>
    private static (int ActivityId, List<int> DayIds) SeedTwoWeekActivity(CedevaWebApplicationFactory factory, bool dayByDay)
    {
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Au Jour Le Jour");
            activity.StartDate = NextMonday();
            activity.EndDate = activity.StartDate.AddDays(11);
            activity.PricePerDay = 20m;
            activity.AllowDayByDayRegistration = dayByDay;
            ActivityDayGenerator.GenerateDays(activity);
            ctx.AddRange(org, activity);
            return 0;
        });
        using var db = factory.NewDbContext();
        var dayIds = db.ActivityDays.IgnoreQueryFilters()
            .Where(d => d.ActivityId == activity.Id && d.IsActive)
            .OrderBy(d => d.DayDate).Select(d => d.DayId).ToList();
        return (activity.Id, dayIds);
    }

    private static Dictionary<string, string> ValidFields(int activityId) => new()
    {
        ["ActivityId"] = activityId.ToString(),
        ["ParentFirstName"] = "Paul",
        ["ParentLastName"] = "Parent",
        ["ParentEmail"] = "paul.jour@test.be",
        ["ParentPhoneNumber"] = "021234567",
        ["ParentNationalRegisterNumber"] = "85.06.15-133.80",
        ["ParentStreet"] = "Rue Publique 1",
        ["ParentPostalCode"] = "1000",
        ["ParentCity"] = "Bruxelles",
        ["ChildFirstName"] = "Enzo",
        ["ChildLastName"] = "Enfant",
        ["ChildBirthDate"] = "2016-07-08",
        ["ChildNationalRegisterNumber"] = "16.07.08-164.10",
    };

    [Fact]
    public async Task Register_Get_DayByDayActivity_ShowsDayCheckboxesInsteadOfWeeks()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (activityId, dayIds) = SeedTwoWeekActivity(factory, dayByDay: true);

        var html = await (await Anonymous(factory).GetAsync($"/PublicRegistration/Register?activityId={activityId}"))
            .Content.ReadAsStringAsync();

        html.Should().Contain($"id=\"day_{dayIds[0]}\"").And.Contain($"id=\"day_{dayIds[^1]}\"");
        html.Should().NotContain("id=\"week_1\"");
    }

    [Fact]
    public async Task Register_Post_DayByDay_BooksOnlyTheTickedDaysAndCharges()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (activityId, dayIds) = SeedTwoWeekActivity(factory, dayByDay: true);
        var fields = ValidFields(activityId);
        fields["SelectedDays[0]"] = dayIds[0].ToString();
        fields["SelectedDays[1]"] = dayIds[7].ToString(); // a day of week 2

        var response = await Anonymous(factory).PostAsync("/PublicRegistration/Register", new FormUrlEncodedContent(fields));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var db = factory.NewDbContext();
        var booking = db.Bookings.IgnoreQueryFilters().Include(b => b.Days).Single(b => b.ActivityId == activityId);
        booking.Days.Select(d => d.ActivityDayId).Should().BeEquivalentTo(new[] { dayIds[0], dayIds[7] });
        booking.TotalAmount.Should().Be(40m);
    }

    [Fact]
    public async Task Register_Post_DayByDay_WithoutAnyDay_IsRejected()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (activityId, _) = SeedTwoWeekActivity(factory, dayByDay: true);

        var response = await Anonymous(factory).PostAsync("/PublicRegistration/Register",
            new FormUrlEncodedContent(ValidFields(activityId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters().Any(b => b.ActivityId == activityId).Should().BeFalse();
    }

    [Fact]
    public async Task Register_Post_WeekActivity_IgnoresPostedDays()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (activityId, dayIds) = SeedTwoWeekActivity(factory, dayByDay: false);
        var fields = ValidFields(activityId);
        fields["SelectedWeeks"] = "1";
        fields["SelectedDays"] = dayIds[7].ToString(); // tampered: a week-2 day

        var response = await Anonymous(factory).PostAsync("/PublicRegistration/Register", new FormUrlEncodedContent(fields));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var db = factory.NewDbContext();
        var booking = db.Bookings.IgnoreQueryFilters().Include(b => b.Days).Single(b => b.ActivityId == activityId);
        booking.Days.Should().HaveCount(5, "week 1 only — posted days are ignored for a week-based activity");
    }
}
