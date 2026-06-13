using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Further coverage for <c>ActivitiesController</c>, targeting branches NOT exercised by
/// <see cref="ActivitiesControllerIntegrationTests"/> or <see cref="ActivitiesControllerCoverageTests"/>:
/// child-collection creation (groups + questions) on Create, postal-code inclusion/exclusion
/// persistence on Create and Edit, existing/new question handling on Edit, date-range expansion,
/// the day activation/deactivation flow (warning + info confirmation branches and the
/// addDaysToBookings path), the route-vs-model id mismatch, and the remaining sort branches.
/// </summary>
[Collection("WebApp")]
public class ActivitiesControllerMoreTests
{
    private static Dictionary<string, string> BaseActivityFields(
        string name = "Stage Plus",
        string description = "Description plus",
        string startDate = "2026-07-01",
        string endDate = "2026-07-05",
        string isActive = "true",
        string pricePerDay = "20",
        int organisationId = 0,
        int id = 0)
    {
        return new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Description"] = description,
            ["StartDate"] = startDate,
            ["EndDate"] = endDate,
            ["IsActive"] = isActive,
            ["PricePerDay"] = pricePerDay,
            ["OrganisationId"] = organisationId.ToString(),
            ["Id"] = id.ToString()
        };
    }

    // ---------- POST Create: child collections (groups + questions) ----------

    [Fact]
    public async Task CreatePost_WithNewGroups_PersistsGroups()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageAvecGroupes");
        fields["NewGroups[0].Label"] = "Les Lutins";
        fields["NewGroups[0].Capacity"] = "12";

        var response = await client.PostAsync("/Activities/Create", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var created = await db.Activities.IgnoreQueryFilters()
            .Include(a => a.Groups)
            .FirstAsync(a => a.Name == "StageAvecGroupes");
        created.Groups.Should().HaveCount(1);
        created.Groups.Single().Label.Should().Be("Les Lutins");
        created.Groups.Single().Capacity.Should().Be(12);
    }

    [Fact]
    public async Task CreatePost_WithNewQuestions_PersistsQuestionsWithDisplayOrder()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageAvecQuestions");
        fields["NewQuestions[0].QuestionText"] = "Allergies ?";
        fields["NewQuestions[0].QuestionType"] = "0"; // Text
        fields["NewQuestions[0].IsRequired"] = "true";

        var response = await client.PostAsync("/Activities/Create", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var created = await db.Activities.IgnoreQueryFilters()
            .Include(a => a.AdditionalQuestions)
            .FirstAsync(a => a.Name == "StageAvecQuestions");
        created.AdditionalQuestions.Should().HaveCount(1);
        var q = created.AdditionalQuestions.Single();
        q.QuestionText.Should().Be("Allergies ?");
        q.IsRequired.Should().BeTrue();
        q.DisplayOrder.Should().Be(1);
        q.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePost_WithPostalCodes_PersistsInclusionAndExclusionLists()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageCodesPostaux");
        fields["IncludedPostalCodes"] = "1000,1050";
        fields["ExcludedPostalCodes"] = "4000";

        var response = await client.PostAsync("/Activities/Create", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var created = await db.Activities.IgnoreQueryFilters()
            .FirstAsync(a => a.Name == "StageCodesPostaux");
        created.IncludedPostalCodes.Should().Be("1000,1050");
        created.ExcludedPostalCodes.Should().Be("4000");
    }

    [Fact]
    public async Task CreatePost_PostalCodesTooLong_ReturnsOkAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageCodesTropLongs");
        // StringLength(500) on IncludedPostalCodes => model error, view re-render (200).
        fields["IncludedPostalCodes"] = new string('1', 501);

        var response = await client.PostAsync("/Activities/Create", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var any = await db.Activities.IgnoreQueryFilters().AnyAsync(a => a.Name == "StageCodesTropLongs");
        any.Should().BeFalse();
    }

    // ---------- POST Edit: postal codes ----------

    [Fact]
    public async Task EditPost_UpdatesPostalCodes()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageCodesEdit");
            activity.IncludedPostalCodes = "1000";
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageCodesEdit", organisationId: org.Id, id: activity.Id);
        fields["IncludedPostalCodes"] = "2000,3000";
        fields["ExcludedPostalCodes"] = "9000";

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var updated = await db.Activities.IgnoreQueryFilters().FirstAsync(a => a.Id == activity.Id);
        updated.IncludedPostalCodes.Should().Be("2000,3000");
        updated.ExcludedPostalCodes.Should().Be("9000");
    }

    // ---------- POST Edit: nested questions are not bound (documented behaviour) ----------

    // NOTE: the Edit POST action has several sibling parameters (id, ActiveDayIds, addDaysToBookings,
    // ...) alongside the unprefixed ActivityViewModel. With that shape the MVC model binder binds the
    // top-level scalars but does NOT populate the nested collections (NewQuestions / ExistingQuestions),
    // so the AddNewQuestionsAsync / UpdateExistingQuestionsAsync branches are effectively unreachable
    // through the real endpoint. These tests pin that observable behaviour: the edit still succeeds
    // (302) but the posted question is silently ignored. (Create, which has a single viewModel
    // parameter, DOES bind NewQuestions/NewGroups — covered above.)

    [Fact]
    public async Task EditPost_PostedNewQuestion_IsNotPersisted()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageNouvelleQuestion");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageNouvelleQuestion", organisationId: org.Id, id: activity.Id);
        fields["NewQuestions[0].QuestionText"] = "Regime alimentaire";
        fields["NewQuestions[0].QuestionType"] = "0";

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var count = await db.Set<ActivityQuestion>().IgnoreQueryFilters()
            .CountAsync(q => q.ActivityId == activity.Id);
        count.Should().Be(0);
    }

    [Fact]
    public async Task EditPost_PostedExistingQuestion_IsNotModified()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        ActivityQuestion question = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageQuestionMaj");
            question = TestData.Question(activity, "Ancien texte");
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageQuestionMaj", organisationId: org.Id, id: activity.Id);
        fields["ExistingQuestions[0].Id"] = question.Id.ToString();
        fields["ExistingQuestions[0].QuestionText"] = "Nouveau texte";
        fields["ExistingQuestions[0].QuestionType"] = "0";
        fields["ExistingQuestions[0].DisplayOrder"] = "1";
        fields["ExistingQuestions[0].IsActive"] = "true";

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var unchanged = await db.Set<ActivityQuestion>().IgnoreQueryFilters().FirstAsync(q => q.Id == question.Id);
        unchanged.QuestionText.Should().Be("Ancien texte");
    }

    // ---------- POST Edit: date-range expansion ----------

    [Fact]
    public async Task EditPost_ExtendingEndDate_AddsMissingDays()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageEtendu"); // 2026-07-01 .. 2026-07-05
            for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
            {
                activity.Days.Add(new ActivityDay { Label = date.ToString("yyyy-MM-dd"), DayDate = date, IsActive = true });
            }
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        // Extend EndDate by two days (to 2026-07-07): HandleDateRangeChanges must add them.
        var fields = BaseActivityFields(
            name: "StageEtendu", startDate: "2026-07-01", endDate: "2026-07-07",
            organisationId: org.Id, id: activity.Id);

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var dayCount = await db.Set<ActivityDay>().IgnoreQueryFilters()
            .CountAsync(d => d.ActivityId == activity.Id);
        dayCount.Should().Be(7);
    }

    // ---------- POST Edit: day activation / deactivation flow ----------

    [Fact]
    public async Task EditPost_DeactivateDayWithoutBookings_RemovesActiveFlag()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int firstDayId = 0;
        var allDayIds = new List<int>();
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageDesactiveJour");
            for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
            {
                activity.Days.Add(new ActivityDay { Label = date.ToString("yyyy-MM-dd"), DayDate = date, IsActive = true });
            }
            ctx.AddRange(org, activity);
            return 0;
        });

        // Capture the seeded day ids.
        using (var db = factory.NewDbContext())
        {
            var days = db.Set<ActivityDay>().IgnoreQueryFilters()
                .Where(d => d.ActivityId == activity.Id).OrderBy(d => d.DayDate).ToList();
            firstDayId = days[0].DayId;
            allDayIds = days.Select(d => d.DayId).ToList();
        }

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageDesactiveJour", organisationId: org.Id, id: activity.Id);
        // ActiveDayIds excludes the first day => it is deactivated (no bookings => no confirmation needed).
        var keep = allDayIds.Where(dayId => dayId != firstDayId).ToList();
        var content = new List<KeyValuePair<string, string>>(
            fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));
        foreach (var dayId in keep)
            content.Add(new KeyValuePair<string, string>("ActiveDayIds", dayId.ToString()));

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(content));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = factory.NewDbContext();
        var firstDay = await verify.Set<ActivityDay>().IgnoreQueryFilters().FirstAsync(d => d.DayId == firstDayId);
        firstDay.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task EditPost_DeactivateDayWithReservedBooking_Unconfirmed_ReturnsViewAndKeepsDay()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int firstDayId = 0;
        var allDayIds = new List<int>();
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageJourReserve");
            for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
            {
                activity.Days.Add(new ActivityDay { Label = date.ToString("yyyy-MM-dd"), DayDate = date, IsActive = true });
            }
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, totalAmount: 100m, paidAmount: 0m);
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        using (var db = factory.NewDbContext())
        {
            var days = db.Set<ActivityDay>().IgnoreQueryFilters()
                .Where(d => d.ActivityId == activity.Id).OrderBy(d => d.DayDate).ToList();
            firstDayId = days[0].DayId;
            allDayIds = days.Select(d => d.DayId).ToList();

            // Reserve the first day for the existing booking.
            var booking = db.Set<Booking>().IgnoreQueryFilters().First(b => b.ActivityId == activity.Id);
            db.Set<BookingDay>().Add(new BookingDay
            {
                BookingId = booking.Id,
                ActivityDayId = firstDayId,
                IsReserved = true,
                IsPresent = false
            });
            db.SaveChanges();
        }

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageJourReserve", organisationId: org.Id, id: activity.Id);
        var keep = allDayIds.Where(dayId => dayId != firstDayId).ToList();
        var content = new List<KeyValuePair<string, string>>(
            fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));
        foreach (var dayId in keep)
            content.Add(new KeyValuePair<string, string>("ActiveDayIds", dayId.ToString()));
        // No removeDaysConfirmed => controller returns the view (200) asking for confirmation.

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(content));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = factory.NewDbContext();
        // Day still active and booking-day still present because the change was not confirmed.
        var firstDay = await verify.Set<ActivityDay>().IgnoreQueryFilters().FirstAsync(d => d.DayId == firstDayId);
        firstDay.IsActive.Should().BeTrue();
        var stillReserved = await verify.Set<BookingDay>().IgnoreQueryFilters()
            .AnyAsync(bd => bd.ActivityDayId == firstDayId);
        stillReserved.Should().BeTrue();
    }

    [Fact]
    public async Task EditPost_DeactivateDayWithReservedBooking_Confirmed_RemovesDayAndBookingDay()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int firstDayId = 0;
        var allDayIds = new List<int>();
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageJourConfirme");
            for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
            {
                activity.Days.Add(new ActivityDay { Label = date.ToString("yyyy-MM-dd"), DayDate = date, IsActive = true });
            }
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, totalAmount: 100m, paidAmount: 0m);
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        using (var db = factory.NewDbContext())
        {
            var days = db.Set<ActivityDay>().IgnoreQueryFilters()
                .Where(d => d.ActivityId == activity.Id).OrderBy(d => d.DayDate).ToList();
            firstDayId = days[0].DayId;
            allDayIds = days.Select(d => d.DayId).ToList();

            var booking = db.Set<Booking>().IgnoreQueryFilters().First(b => b.ActivityId == activity.Id);
            db.Set<BookingDay>().Add(new BookingDay
            {
                BookingId = booking.Id,
                ActivityDayId = firstDayId,
                IsReserved = true,
                IsPresent = false
            });
            db.SaveChanges();
        }

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageJourConfirme", organisationId: org.Id, id: activity.Id);
        var keep = allDayIds.Where(dayId => dayId != firstDayId).ToList();
        var content = new List<KeyValuePair<string, string>>(
            fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));
        foreach (var dayId in keep)
            content.Add(new KeyValuePair<string, string>("ActiveDayIds", dayId.ToString()));
        content.Add(new KeyValuePair<string, string>("removeDaysConfirmed", "true"));

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(content));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = factory.NewDbContext();
        var firstDay = await verify.Set<ActivityDay>().IgnoreQueryFilters().FirstAsync(d => d.DayId == firstDayId);
        firstDay.IsActive.Should().BeFalse();
        var bookingDayGone = await verify.Set<BookingDay>().IgnoreQueryFilters()
            .AnyAsync(bd => bd.ActivityDayId == firstDayId);
        bookingDayGone.Should().BeFalse();
    }

    [Fact]
    public async Task EditPost_ActivateDayWithBookings_NotAdded_ReturnsViewForInfo()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int inactiveDayId = 0;
        var activeDayIds = new List<int>();
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageActiveJour");
            var first = true;
            for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
            {
                activity.Days.Add(new ActivityDay
                {
                    Label = date.ToString("yyyy-MM-dd"),
                    DayDate = date,
                    IsActive = !first // first day starts inactive so it can be activated
                });
                first = false;
            }
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, totalAmount: 100m, paidAmount: 0m);
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        using (var db = factory.NewDbContext())
        {
            var days = db.Set<ActivityDay>().IgnoreQueryFilters()
                .Where(d => d.ActivityId == activity.Id).OrderBy(d => d.DayDate).ToList();
            inactiveDayId = days.First(d => !d.IsActive).DayId;
            activeDayIds = days.Where(d => d.IsActive).Select(d => d.DayId).ToList();
        }

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageActiveJour", organisationId: org.Id, id: activity.Id);
        var content = new List<KeyValuePair<string, string>>(
            fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));
        // Activate the previously inactive day along with the already-active ones.
        foreach (var dayId in activeDayIds)
            content.Add(new KeyValuePair<string, string>("ActiveDayIds", dayId.ToString()));
        content.Add(new KeyValuePair<string, string>("ActiveDayIds", inactiveDayId.ToString()));
        // addDaysToBookings not "true" + bookings exist => info view (200).

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(content));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EditPost_ActivateDay_AddDaysToBookings_AddsBookingDay()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int inactiveDayId = 0;
        var activeDayIds = new List<int>();
        int bookingId = 0;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageActiveAjoute");
            var first = true;
            for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
            {
                activity.Days.Add(new ActivityDay
                {
                    Label = date.ToString("yyyy-MM-dd"),
                    DayDate = date,
                    IsActive = !first
                });
                first = false;
            }
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, totalAmount: 100m, paidAmount: 0m);
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        using (var db = factory.NewDbContext())
        {
            var days = db.Set<ActivityDay>().IgnoreQueryFilters()
                .Where(d => d.ActivityId == activity.Id).OrderBy(d => d.DayDate).ToList();
            inactiveDayId = days.First(d => !d.IsActive).DayId;
            activeDayIds = days.Where(d => d.IsActive).Select(d => d.DayId).ToList();
            bookingId = db.Set<Booking>().IgnoreQueryFilters().First(b => b.ActivityId == activity.Id).Id;
        }

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var fields = BaseActivityFields(name: "StageActiveAjoute", organisationId: org.Id, id: activity.Id);
        var content = new List<KeyValuePair<string, string>>(
            fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));
        foreach (var dayId in activeDayIds)
            content.Add(new KeyValuePair<string, string>("ActiveDayIds", dayId.ToString()));
        content.Add(new KeyValuePair<string, string>("ActiveDayIds", inactiveDayId.ToString()));
        content.Add(new KeyValuePair<string, string>("addDaysToBookings", "true"));

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(content));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var verify = factory.NewDbContext();
        var added = await verify.Set<BookingDay>().IgnoreQueryFilters()
            .AnyAsync(bd => bd.BookingId == bookingId && bd.ActivityDayId == inactiveDayId);
        added.Should().BeTrue();
    }

    // ---------- POST Edit: route-vs-model id mismatch ----------

    [Fact]
    public async Task EditPost_RouteIdDiffersFromModelId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "StageMismatch");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        // Route id (activity.Id) but model Id forced to a different value => id != viewModel.Id => NotFound.
        var fields = BaseActivityFields(name: "StageMismatch", organisationId: org.Id, id: activity.Id + 1);

        var response = await client.PostAsync($"/Activities/Edit/{activity.Id}", new FormUrlEncodedContent(fields));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- GET Index: remaining sort branches ----------

    [Fact]
    public async Task Index_SortByStartDateDescending_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var early = TestData.Activity(org, "StageTot");
            early.StartDate = new DateTime(2026, 1, 1);
            early.EndDate = new DateTime(2026, 1, 5);
            var late = TestData.Activity(org, "StageTard");
            late.StartDate = new DateTime(2026, 12, 1);
            late.EndDate = new DateTime(2026, 12, 5);
            ctx.AddRange(org, early, late);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var redirect = await client.GetAsync("/Activities?sortBy=startdate&sortOrder=desc");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var listed = await client.GetAsync("/Activities");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await listed.Content.ReadAsStringAsync();
        // Descending by start date => the December activity appears before the January one.
        html.IndexOf("StageTard", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("StageTot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Index_SortByEndDateAscending_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.AddRange(org, TestData.Activity(org, "StageFinTri"));
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var redirect = await client.GetAsync("/Activities?sortBy=enddate&sortOrder=asc");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var listed = await client.GetAsync("/Activities");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await listed.Content.ReadAsStringAsync()).Should().Contain("StageFinTri");
    }

    [Fact]
    public async Task Index_SortByIsActiveDescending_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.AddRange(org, TestData.Activity(org, "StageActifTri"));
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var redirect = await client.GetAsync("/Activities?sortBy=isactive&sortOrder=desc");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var listed = await client.GetAsync("/Activities");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await listed.Content.ReadAsStringAsync()).Should().Contain("StageActifTri");
    }
}
