using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Helpers;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage for public registration's week picker on multi-week activities: a child registers for
/// one or more specific weeks, not necessarily the whole (possibly multi-week) activity — both the
/// simple iframe flow (<c>Register</c>) and the multi-step wizard
/// (<c>SelectActivity</c>/.../<c>ActivityQuestions</c>/<c>CreateBooking</c>).
/// </summary>
[Collection("WebApp")]
public class PublicRegistrationWeekSelectionTests
{
    private const string ParentNrn = "85.06.15-133.80";
    private const string ChildNrn = "16.07.08-164.10";

    private static HttpClient Anonymous(CedevaWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent Form(Dictionary<string, string> fields) => new(fields);

    /// <summary>Next Monday at least one month out, so activities stay "future" regardless of when the suite runs.</summary>
    private static DateTime NextMonday()
    {
        var date = DateTime.Today.AddMonths(1);
        while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(1);
        return date;
    }

    /// <summary>Two-week activity (Mon-Fri x2, weekend excluded), 20€/day.</summary>
    private static (int OrgId, int ActivityId) SeedTwoWeekActivity(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Deux Semaines");
            activity.StartDate = NextMonday();
            activity.EndDate = activity.StartDate.AddDays(11); // Friday of the following week
            activity.PricePerDay = 20m;
            ActivityDayGenerator.GenerateDays(activity);
            ctx.AddRange(org, activity);
            return 0;
        });
        return (org.Id, activity.Id);
    }

    private static (int OrgId, int ActivityId) SeedOneWeekActivity(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Une Semaine");
            activity.StartDate = NextMonday();
            activity.EndDate = activity.StartDate.AddDays(4); // Friday of the same week
            activity.PricePerDay = 20m;
            ActivityDayGenerator.GenerateDays(activity);
            ctx.AddRange(org, activity);
            return 0;
        });
        return (org.Id, activity.Id);
    }

    private static Dictionary<string, string> ValidSimpleFields(int activityId) => new()
    {
        ["ActivityId"] = activityId.ToString(),
        ["ParentFirstName"] = "Paul",
        ["ParentLastName"] = "Parent",
        ["ParentEmail"] = "paul.week@test.be",
        ["ParentPhoneNumber"] = "021234567",
        ["ParentNationalRegisterNumber"] = ParentNrn,
        ["ParentStreet"] = "Rue Publique 1",
        ["ParentPostalCode"] = "1000",
        ["ParentCity"] = "Bruxelles",
        ["ChildFirstName"] = "Enzo",
        ["ChildLastName"] = "Enfant",
        ["ChildBirthDate"] = "2016-07-08",
        ["ChildNationalRegisterNumber"] = ChildNrn,
    };

    // =====================================================================
    // Simple flow (Register) — GET
    // =====================================================================

    [Fact]
    public async Task Register_Get_TwoWeekActivity_ShowsBothWeekCheckboxes()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedTwoWeekActivity(factory);
        var client = Anonymous(factory);

        var response = await client.GetAsync($"/PublicRegistration/Register?activityId={activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("week_1");
        html.Should().Contain("week_2");
    }

    [Fact]
    public async Task Register_Get_OneWeekActivity_DoesNotShowWeekPicker()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedOneWeekActivity(factory);
        var client = Anonymous(factory);

        var response = await client.GetAsync($"/PublicRegistration/Register?activityId={activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("week_1");
    }

    // =====================================================================
    // Simple flow (Register) — POST
    // =====================================================================

    [Fact]
    public async Task Register_Post_SelectOneOfTwoWeeks_ReservesOnlyThatWeeksDaysAndAmount()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedTwoWeekActivity(factory);
        var client = Anonymous(factory);

        var fields = ValidSimpleFields(activityId);
        fields["SelectedWeeks"] = "1";

        var response = await client.PostAsync("/PublicRegistration/Register", Form(fields));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Confirmation");

        using var db = factory.NewDbContext();
        var booking = db.Bookings.IgnoreQueryFilters().Include(b => b.Days).Single(b => b.ActivityId == activityId);
        booking.Days.Should().HaveCount(5, "week 1 has 5 weekdays (Mon-Fri)");
        booking.TotalAmount.Should().Be(100m, "20€/day x 5 days of week 1 only");

        var activity = db.Activities.IgnoreQueryFilters()
            .Include(a => a.Days)
            .Single(a => a.Id == activityId);
        var week2DayIds = activity.Days.Where(d => d.Week == 2).Select(d => d.DayId).ToHashSet();
        booking.Days.Should().NotContain(d => week2DayIds.Contains(d.ActivityDayId), "week 2 was not selected");
    }

    [Fact]
    public async Task Register_Post_SelectBothWeeks_ReservesAllTenDays()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedTwoWeekActivity(factory);
        var client = Anonymous(factory);

        var fields = ValidSimpleFields(activityId);
        fields["SelectedWeeks[0]"] = "1";
        fields["SelectedWeeks[1]"] = "2";

        var response = await client.PostAsync("/PublicRegistration/Register", Form(fields));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var booking = db.Bookings.IgnoreQueryFilters().Include(b => b.Days).Single(b => b.ActivityId == activityId);
        booking.Days.Should().HaveCount(10);
        booking.TotalAmount.Should().Be(200m);
    }

    [Fact]
    public async Task Register_Post_TwoWeekActivity_NoWeekSelected_ReturnsOkAndCreatesNoBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedTwoWeekActivity(factory);
        var client = Anonymous(factory);

        var response = await client.PostAsync("/PublicRegistration/Register", Form(ValidSimpleFields(activityId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters().Any(b => b.ActivityId == activityId).Should().BeFalse();
    }

    [Fact]
    public async Task Register_Post_OneWeekActivity_NoWeeksField_StillReservesAllDays()
    {
        // Backward compatibility: a single-week activity has nothing to choose, so the absence of
        // any SelectedWeeks field must not block the registration (as it did before this feature).
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedOneWeekActivity(factory);
        var client = Anonymous(factory);

        var response = await client.PostAsync("/PublicRegistration/Register", Form(ValidSimpleFields(activityId)));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var booking = db.Bookings.IgnoreQueryFilters().Include(b => b.Days).Single(b => b.ActivityId == activityId);
        booking.Days.Should().HaveCount(5);
        booking.TotalAmount.Should().Be(100m);
    }

    // =====================================================================
    // Multi-step wizard (SelectActivity -> ... -> ActivityQuestions -> CreateBooking)
    // =====================================================================

    private static FormUrlEncodedContent ParentForm(int activityId) =>
        Form(new()
        {
            ["ActivityId"] = activityId.ToString(),
            ["FirstName"] = "Paul",
            ["LastName"] = "Parent",
            ["Email"] = "wizard.week@test.be",
            ["PhoneNumber"] = "021234567",
            ["MobilePhoneNumber"] = "0470000000",
            ["NationalRegisterNumber"] = ParentNrn,
            ["Street"] = "Rue Wizard 1",
            ["PostalCode"] = "1000",
            ["City"] = "Bruxelles",
        });

    private static FormUrlEncodedContent ChildForm(int activityId, int parentId) =>
        Form(new()
        {
            ["ActivityId"] = activityId.ToString(),
            ["ParentId"] = parentId.ToString(),
            ["FirstName"] = "Enzo",
            ["LastName"] = "Enfant",
            ["BirthDate"] = "2016-07-08",
            ["NationalRegisterNumber"] = ChildNrn,
        });

    private static int NewestParentId(CedevaWebApplicationFactory factory)
    {
        using var db = factory.NewDbContext();
        return db.Parents.IgnoreQueryFilters().OrderByDescending(p => p.Id).First().Id;
    }

    private static async Task DriveToActivityQuestionsGetAsync(
        HttpClient client, CedevaWebApplicationFactory factory, int orgId, int activityId)
    {
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={orgId}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activityId.ToString() }));
        await client.PostAsync("/PublicRegistration/ParentInformation", ParentForm(activityId));
        await client.GetAsync("/PublicRegistration/ChildInformation");
        await client.PostAsync("/PublicRegistration/ChildInformation",
            ChildForm(activityId, NewestParentId(factory)));
    }

    [Fact]
    public async Task ActivityQuestions_Get_TwoWeekActivity_NoQuestions_DoesNotSkipAndShowsWeeks()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedTwoWeekActivity(factory);
        var client = Anonymous(factory);
        await DriveToActivityQuestionsGetAsync(client, factory, orgId, activityId);

        var response = await client.GetAsync("/PublicRegistration/ActivityQuestions");

        // Would redirect to CreateBooking if the picker weren't needed — must render instead.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("week_1");
        html.Should().Contain("week_2");
    }

    [Fact]
    public async Task ActivityQuestions_Post_SelectedWeek_ReservesOnlyThatWeeksDays()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedTwoWeekActivity(factory);
        var client = Anonymous(factory);
        await DriveToActivityQuestionsGetAsync(client, factory, orgId, activityId);
        await client.GetAsync("/PublicRegistration/ActivityQuestions");

        var post = await client.PostAsync("/PublicRegistration/ActivityQuestions", Form(new()
        {
            ["ActivityId"] = activityId.ToString(),
            ["SelectedWeeks"] = "2",
        }));

        post.StatusCode.Should().Be(HttpStatusCode.Found);
        post.Headers.Location!.ToString().Should().Contain("CreateBooking");

        var createBooking = await client.GetAsync(post.Headers.Location!.ToString());
        createBooking.StatusCode.Should().Be(HttpStatusCode.Found);
        createBooking.Headers.Location!.ToString().Should().Contain("Confirmation");

        using var db = factory.NewDbContext();
        var booking = db.Bookings.IgnoreQueryFilters().Include(b => b.Days).Single(b => b.ActivityId == activityId);
        booking.Days.Should().HaveCount(5);
        booking.TotalAmount.Should().Be(100m);
    }

    // =====================================================================
    // Birth-year quota (Lot K #4) enforced PER WEEK, not per whole activity.
    // =====================================================================

    [Fact]
    public async Task Register_Post_BirthYearQuotaFullForWeek1_StillAllowsWeek2Registration()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedTwoWeekActivity(factory);
        factory.Seed(ctx =>
        {
            var activity = ctx.Activities.IgnoreQueryFilters().Include(a => a.Days).Single(a => a.Id == activityId);
            var week1Days = activity.Days.Where(d => d.Week == 1).ToList();

            var quota = new ActivityBirthYearQuota { ActivityId = activityId, BirthYear = 2016, MaxChildren = 1 };
            var parent = TestData.Parent(ctx.Organisations.IgnoreQueryFilters().Single(o => o.Id == orgId));
            var child = TestData.Child(parent); // BirthDate 2016-05-20 (TestData default)
            var booking = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 0m);
            booking.Days = week1Days.Select(d => new BookingDay { ActivityDay = d, IsReserved = true }).ToList();

            ctx.AddRange(quota, parent, child, booking);
            return 0;
        });

        var client = Anonymous(factory);
        var fields = ValidSimpleFields(activityId);
        fields["ChildBirthDate"] = "2016-03-10"; // same birth year (2016) as the existing week-1 registrant
        fields["SelectedWeeks"] = "2"; // registering for week 2 only, which has no birth-2016 registrant yet

        var response = await client.PostAsync("/PublicRegistration/Register", Form(fields));

        response.StatusCode.Should().Be(HttpStatusCode.Found,
            "the quota is full for week 1 only — week 2 has room for this birth year");

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters().Count(b => b.ActivityId == activityId).Should().Be(2);
    }

    [Fact]
    public async Task Register_Post_BirthYearQuotaFullForWeek1_BlocksAnotherWeek1Registration()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedTwoWeekActivity(factory);
        factory.Seed(ctx =>
        {
            var activity = ctx.Activities.IgnoreQueryFilters().Include(a => a.Days).Single(a => a.Id == activityId);
            var week1Days = activity.Days.Where(d => d.Week == 1).ToList();

            var quota = new ActivityBirthYearQuota { ActivityId = activityId, BirthYear = 2016, MaxChildren = 1 };
            var parent = TestData.Parent(ctx.Organisations.IgnoreQueryFilters().Single(o => o.Id == orgId));
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 0m);
            booking.Days = week1Days.Select(d => new BookingDay { ActivityDay = d, IsReserved = true }).ToList();

            ctx.AddRange(quota, parent, child, booking);
            return 0;
        });

        var client = Anonymous(factory);
        var fields = ValidSimpleFields(activityId);
        fields["ChildBirthDate"] = "2016-03-10";
        fields["SelectedWeeks"] = "1"; // same week as the existing registrant -> quota full

        var response = await client.PostAsync("/PublicRegistration/Register", Form(fields));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "week 1's quota for this birth year is already reached");

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters().Count(b => b.ActivityId == activityId).Should().Be(1);
    }

    [Fact]
    public async Task ActivityQuestions_Post_TwoWeekActivity_NoWeekSelected_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedTwoWeekActivity(factory);
        var client = Anonymous(factory);
        await DriveToActivityQuestionsGetAsync(client, factory, orgId, activityId);
        await client.GetAsync("/PublicRegistration/ActivityQuestions");

        var post = await client.PostAsync("/PublicRegistration/ActivityQuestions", Form(new()
        {
            ["ActivityId"] = activityId.ToString(),
        }));

        post.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters().Any(b => b.ActivityId == activityId).Should().BeFalse();
    }
}
