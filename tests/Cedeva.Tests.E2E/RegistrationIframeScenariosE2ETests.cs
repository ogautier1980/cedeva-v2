using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Extra browser scenarios for the public registration iframe beyond the happy path
/// (<see cref="RegistrationFlowTests"/>): colour customisation via query params, custom activity
/// questions, duplicate-child detection, and the online-payment offer after a real registration.
/// All drive the single-page simple form, anonymously, like an embedded partner site.
/// </summary>
[Collection("E2E")]
public class RegistrationIframeScenariosE2ETests
{
    private readonly PlaywrightFixture _fx;

    public RegistrationIframeScenariosE2ETests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>Seeds an active activity with two active days (so a booking has a non-zero total) and
    /// optionally one required text question. Returns (activityId, questionId).</summary>
    private (int ActivityId, int QuestionId) SeedActivity(bool withQuestion)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        return _fx.Factory.Seed(ctx =>
        {
            var start = DateTime.Today.AddMonths(2);
            while (start.DayOfWeek != DayOfWeek.Monday) start = start.AddDays(1);

            var activity = new Activity
            {
                Name = $"Iframe-{tag}",
                Description = "Iframe scenarios",
                IsActive = true,
                PricePerDay = 20m,
                StartDate = start,
                EndDate = start.AddDays(1),
                OrganisationId = _fx.OrganisationId,
                Days =
                {
                    new ActivityDay { Label = "J1", DayDate = start, Week = 1, IsActive = true },
                    new ActivityDay { Label = "J2", DayDate = start.AddDays(1), Week = 1, IsActive = true },
                }
            };
            int questionId = 0;
            if (withQuestion)
            {
                var q = new ActivityQuestion
                {
                    Activity = activity,
                    QuestionText = "Allergies ?",
                    QuestionType = QuestionType.Text,
                    IsRequired = true,
                    IsActive = true,
                    DisplayOrder = 1
                };
                ctx.ActivityQuestions.Add(q);
                ctx.Activities.Add(activity);
                ctx.SaveChanges();
                return (activity.Id, q.Id);
            }
            ctx.Activities.Add(activity);
            ctx.SaveChanges();
            return (activity.Id, 0);
        });
    }

    private static async Task FillSimpleFormAsync(IPage page, string childNrn, string parentNrn = "85.06.15-133.80", string? parentEmail = null)
    {
        await page.FillAsync("#ParentFirstName", "Marc");
        await page.FillAsync("#ParentLastName", "Dupont");
        await page.FillAsync("#ParentEmail", parentEmail ?? $"marc.{Guid.NewGuid():N}@test.be");
        await page.FillAsync("#ParentPhoneNumber", "0470000000");
        await page.FillAsync("#ParentStreet", "Rue de Test 1");
        await page.FillAsync("#ParentPostalCode", "1000");
        await page.FillAsync("#ParentCity", "Bruxelles");
        await page.FillAsync("#ParentNationalRegisterNumber", parentNrn);
        await page.FillAsync("#ChildFirstName", "Lou");
        await page.FillAsync("#ChildLastName", "Dupont");
        await page.FillAsync("#ChildBirthDate", "2016-07-08");
        await page.FillAsync("#ChildNationalRegisterNumber", childNrn);
    }

    [Fact]
    public async Task Register_WithColorQueryParams_AppliesCustomColors()
    {
        var page = await _fx.Browser.NewPageAsync();
        var response = await page.GotoAsync(
            $"{_fx.BaseUrl}/PublicRegistration/Register?activityId={_fx.ActivityId}&bg=ffcc00&btn=cc0044");

        response!.Status.Should().Be(200);
        var html = await page.ContentAsync();
        html.Should().Contain("ffcc00", "the bg query param drives the background colour");
        html.Should().Contain("cc0044", "the btn query param drives the button colour");
    }

    [Fact]
    public async Task Register_WithCustomQuestion_PersistsTheAnswer()
    {
        var (activityId, questionId) = SeedActivity(withQuestion: true);

        var page = await _fx.Browser.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/PublicRegistration/Register?activityId={activityId}");

        // The custom question renders as QuestionAnswers[{id}].
        (await page.Locator($"[name='QuestionAnswers[{questionId}]']").CountAsync())
            .Should().Be(1, "the activity's custom question should appear on the form");

        await FillSimpleFormAsync(page, childNrn: "16.07.08-164.10");
        await page.FillAsync($"[name='QuestionAnswers[{questionId}]']", "Aucune allergie connue");
        await page.ClickAsync("button[type=submit]");
        await page.WaitForURLAsync("**/PublicRegistration/Confirmation**");

        await using var db = _fx.Factory.NewDbContext();
        (await db.ActivityQuestionAnswers.IgnoreQueryFilters()
            .AnyAsync(a => a.AnswerText == "Aucune allergie connue"))
            .Should().BeTrue("the custom-question answer must be persisted");
    }

    [Fact]
    public async Task Register_SameChildTwice_DoesNotDuplicate()
    {
        var (activityId, _) = SeedActivity(withQuestion: false);
        const string childNrn = "16.07.08-164.10";
        const string parentNrn = "85.06.15-133.80";
        var parentEmail = $"dup.{Guid.NewGuid():N}@test.be"; // same parent across both registrations

        // First registration succeeds → confirmation page.
        var page1 = await _fx.Browser.NewPageAsync();
        await page1.GotoAsync($"{_fx.BaseUrl}/PublicRegistration/Register?activityId={activityId}");
        await FillSimpleFormAsync(page1, childNrn, parentNrn, parentEmail);
        await page1.ClickAsync("button[type=submit]");
        await page1.WaitForURLAsync("**/PublicRegistration/Confirmation**");

        // Second registration (same parent + child) is rejected as a duplicate and stays on the form.
        var page2 = await _fx.Browser.NewPageAsync();
        await page2.GotoAsync($"{_fx.BaseUrl}/PublicRegistration/Register?activityId={activityId}");
        await FillSimpleFormAsync(page2, childNrn, parentNrn, parentEmail);
        await page2.ClickAsync("button[type=submit]");
        await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);
        page2.Url.Should().Contain("/PublicRegistration/Register", "a duplicate registration must not reach confirmation");

        await using var db = _fx.Factory.NewDbContext();
        // Format-independent: this freshly-seeded activity only ever had this one child registered.
        var bookings = await db.Bookings.IgnoreQueryFilters()
            .Where(b => b.ActivityId == activityId).ToListAsync();
        bookings.Should().HaveCount(1, "the duplicate registration must not create a second booking");
        bookings.Select(b => b.ChildId).Distinct().Should().HaveCount(1,
            "registering the same child twice must update, not duplicate the child");
    }

    [Fact]
    public async Task Register_OnActivityWithDays_OffersOnlinePaymentOnConfirmation()
    {
        var (activityId, _) = SeedActivity(withQuestion: false);

        var page = await _fx.Browser.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/PublicRegistration/Register?activityId={activityId}");
        await FillSimpleFormAsync(page, childNrn: "16.07.08-164.10");
        await page.ClickAsync("button[type=submit]");
        await page.WaitForURLAsync("**/PublicRegistration/Confirmation**");

        // 2 active days x 20€ => balance due => the "Pay online" button is offered.
        var payLink = page.Locator("a[href*='/OnlinePayment/Checkout']");
        (await payLink.CountAsync()).Should().Be(1,
            "a registration with a non-zero total should offer online payment (proves TotalAmount > 0)");
    }
}
