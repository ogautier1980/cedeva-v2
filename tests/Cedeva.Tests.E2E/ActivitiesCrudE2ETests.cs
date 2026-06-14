using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser CRUD coverage for the Activities feature, driven as a Coordinator scoped to the seeded
/// organisation. Exercises the real Razor forms (Create/Edit/Delete) end-to-end and asserts the
/// resulting persisted state via a fresh DbContext (IgnoreQueryFilters, since the assert scope has
/// no tenant). Every test uses unique activity names so the shared sequential DB stays isolated.
/// </summary>
[Collection("E2E")]
public class ActivitiesCrudE2ETests
{
    private readonly PlaywrightFixture _fx;

    public ActivitiesCrudE2ETests(PlaywrightFixture fx) => _fx = fx;

    [Fact]
    public async Task Create_RendersForm_Returns200()
    {
        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Activities/Create");

        response!.Status.Should().Be(200);
        (await page.Locator("#Name").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#PricePerDay").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("button[type=submit]:not(.btn-link):not(.dropdown-item)").First.IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Create_ValidActivity_PersistsToDatabase()
    {
        var name = $"Stage-{Guid.NewGuid():N}";
        var start = DateTime.Today.AddMonths(3);
        var end = start.AddDays(4);

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Activities/Create");

        await page.FillAsync("#Name", name);
        await page.FillAsync("#Description", "Description E2E creation");
        await page.FillAsync("#StartDate", start.ToString("yyyy-MM-dd"));
        await page.FillAsync("#EndDate", end.ToString("yyyy-MM-dd"));
        await page.FillAsync("#PricePerDay", "25");
        await page.FillAsync("#IncludedPostalCodes", "1000,1050");
        await page.FillAsync("#ExcludedPostalCodes", "9000");

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync($"{_fx.BaseUrl}/Activities", new PageWaitForURLOptions { Timeout = 15000 });

        await using var db = _fx.Factory.NewDbContext();
        var saved = await db.Activities
            .IgnoreQueryFilters()
            .Include(a => a.Days)
            .FirstOrDefaultAsync(a => a.Name == name);

        saved.Should().NotBeNull("the valid activity should have been persisted");
        saved!.OrganisationId.Should().Be(_fx.OrganisationId);
        saved.PricePerDay.Should().Be(25m);
        saved.IncludedPostalCodes.Should().Be("1000,1050");
        saved.ExcludedPostalCodes.Should().Be("9000");
        saved.StartDate.Date.Should().Be(start);
        saved.EndDate.Date.Should().Be(end);
        // The controller generates one ActivityDay per calendar day in the range (inclusive).
        saved.Days.Should().HaveCount(5);
    }

    [Fact]
    public async Task Create_MissingName_ShowsValidationAndDoesNotPersist()
    {
        var uniqueDescription = $"Invalid-{Guid.NewGuid():N}";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Activities/Create");

        // Leave Name empty -> required-field validation must block the post.
        await page.FillAsync("#Description", uniqueDescription);
        await page.FillAsync("#StartDate", DateTime.Today.AddMonths(3).ToString("yyyy-MM-dd"));
        await page.FillAsync("#EndDate", DateTime.Today.AddMonths(3).AddDays(2).ToString("yyyy-MM-dd"));

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Still on the Create form (client-side jQuery validation prevents navigation).
        page.Url.Should().Contain("/Activities/Create");
        (await page.Locator("span[data-valmsg-for=\"Name\"]").IsVisibleAsync()).Should().BeTrue();

        await using var db = _fx.Factory.NewDbContext();
        (await db.Activities.IgnoreQueryFilters()
            .AnyAsync(a => a.Description == uniqueDescription))
            .Should().BeFalse("an activity with no name must not be persisted");
    }

    [Fact]
    public async Task Edit_RenamesAndChangesPrice_Persists()
    {
        var originalName = $"Edit-{Guid.NewGuid():N}";
        var start = DateTime.Today.AddMonths(4);
        var end = start.AddDays(3);

        // Seed the activity directly so the test starts from a known DB state.
        var activityId = _fx.Factory.Seed(db =>
        {
            var activity = new Cedeva.Core.Entities.Activity
            {
                Name = originalName,
                Description = "Original description",
                IsActive = true,
                PricePerDay = 10m,
                StartDate = start,
                EndDate = end,
                OrganisationId = _fx.OrganisationId
            };
            db.Activities.Add(activity);
            db.SaveChanges();
            return activity.Id;
        });

        var newName = $"Edited-{Guid.NewGuid():N}";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/Activities/Edit/{activityId}");
        response!.Status.Should().Be(200);

        await page.FillAsync("#Name", newName);
        await page.FillAsync("#PricePerDay", "30");

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync($"{_fx.BaseUrl}/Activities", new PageWaitForURLOptions { Timeout = 15000 });

        await using var db2 = _fx.Factory.NewDbContext();
        var updated = await db2.Activities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == activityId);

        updated.Should().NotBeNull();
        updated!.Name.Should().Be(newName);
        updated.PricePerDay.Should().Be(30m);
    }

    [Fact]
    public async Task Delete_RemovesActivityWithoutBookings()
    {
        var name = $"Delete-{Guid.NewGuid():N}";
        var start = DateTime.Today.AddMonths(5);

        var activityId = _fx.Factory.Seed(db =>
        {
            var activity = new Cedeva.Core.Entities.Activity
            {
                Name = name,
                Description = "To be deleted",
                IsActive = true,
                PricePerDay = 15m,
                StartDate = start,
                EndDate = start.AddDays(2),
                OrganisationId = _fx.OrganisationId
            };
            db.Activities.Add(activity);
            db.SaveChanges();
            return activity.Id;
        });

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/Activities/Delete/{activityId}");
        response!.Status.Should().Be(200);

        // The confirm form only renders when there are no bookings.
        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync($"{_fx.BaseUrl}/Activities", new PageWaitForURLOptions { Timeout = 15000 });

        await using var db2 = _fx.Factory.NewDbContext();
        (await db2.Activities.IgnoreQueryFilters().AnyAsync(a => a.Id == activityId))
            .Should().BeFalse("the activity should be deleted");
    }
}
