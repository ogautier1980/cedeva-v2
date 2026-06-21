using Cedeva.Core.Entities;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser coverage of the live day-range editor on the activity Edit page: clicking "+ day after"
/// calls the AJAX endpoint and updates the end-date input and the day list in place (no full reload).
/// </summary>
[Collection("E2E")]
public class ActivityDayEditorE2ETests
{
    private readonly PlaywrightFixture _fx;

    public ActivityDayEditorE2ETests(PlaywrightFixture fx) => _fx = fx;

    private int SeedActivityWithDays()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        return _fx.Factory.Seed(ctx =>
        {
            var start = DateTime.Today.AddMonths(3);
            while (start.DayOfWeek != DayOfWeek.Monday) start = start.AddDays(1);
            var activity = new Activity
            {
                Name = $"DayEdit-{tag}",
                Description = "Day editor E2E",
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
            ctx.Activities.Add(activity);
            ctx.SaveChanges();
            return activity.Id;
        });
    }

    [Fact]
    public async Task AddDayAfter_UpdatesEndDateInputLive()
    {
        var activityId = SeedActivityWithDays();

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Activities/Edit/{activityId}");

        var endBefore = await page.InputValueAsync("#EndDate");

        // Click "+ day after" and wait for the AJAX adjust to complete.
        var respTask = page.WaitForResponseAsync(r =>
            r.Url.Contains("/Activities/AdjustActivityDays", StringComparison.OrdinalIgnoreCase) && r.Status == 200);
        await page.ClickAsync("button[data-day-op='extend'][data-day-edge='end']");
        await respTask;

        // The end-date input updates live (one day later) without a full page reload.
        await page.WaitForFunctionAsync(
            "([sel, before]) => document.querySelector(sel).value !== before",
            new object[] { "#EndDate", endBefore });

        var endAfter = await page.InputValueAsync("#EndDate");
        DateTime.Parse(endAfter).Should().Be(DateTime.Parse(endBefore).AddDays(1));
    }
}
