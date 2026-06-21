using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Loads the main authenticated views in a real browser and fails if any of them logs a console
/// error or raises an uncaught JS exception — the class of regression (broken script, missing
/// resource, CSP block, JS throw) that server-side tests cannot see. Benign resource noise
/// (favicon, source maps) is filtered out so only real problems fail the audit.
/// </summary>
[Collection("E2E")]
public class ConsoleAuditE2ETests
{
    private readonly PlaywrightFixture _fx;

    public ConsoleAuditE2ETests(PlaywrightFixture fx) => _fx = fx;

    private static bool IsBenign(string text) =>
        text.Contains("favicon", StringComparison.OrdinalIgnoreCase) ||
        text.Contains(".map", StringComparison.OrdinalIgnoreCase) ||
        // Test-harness only: the X-Test-User auth header is sent on every request (incl. cross-origin
        // CDN font fetches), so cdnjs rejects the CORS preflight and the fonts fail to load. There is
        // no such header in production, so this is not a real defect. Genuine same-origin asset 404s
        // ("status of 404") and JS exceptions are still caught.
        text.Contains("CORS policy", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("net::ERR_FAILED", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("cdnjs.cloudflare", StringComparison.OrdinalIgnoreCase);

    private async Task<List<string>> CollectErrorsAsync(IBrowserContext ctx, string path)
    {
        var page = await ctx.NewPageAsync();
        var errors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error" && !IsBenign(msg.Text))
                errors.Add($"{path} [console] {msg.Text}");
        };
        page.PageError += (_, err) =>
        {
            if (!IsBenign(err))
                errors.Add($"{path} [pageerror] {err}");
        };

        var response = await page.GotoAsync($"{_fx.BaseUrl}{path}");
        if (response is { Status: >= 400 })
            errors.Add($"{path} [http] status {response.Status}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.CloseAsync();
        return errors;
    }

    [Fact]
    public async Task AuthenticatedViews_HaveNoConsoleErrors()
    {
        var coordinatorPages = new[]
        {
            "/",
            "/Activities", "/Activities/Create",
            "/Bookings", "/Bookings/Create",
            "/Children", "/Children/Create",
            "/Parents", "/Parents/Create",
            "/TeamMembers", "/TeamMembers/Create",
            "/EmailTemplates", "/EmailTemplates/Create",
            "/Payments",
            "/Account/Profile",
            $"/Excursions?id={_fx.ActivityId}",
            $"/Excursions/Create/{_fx.ActivityId}",
            $"/ActivityManagement/SendEmail/{_fx.ActivityId}",
            $"/ActivityManagement/Presences?id={_fx.ActivityId}",
            $"/ActivityQuestions?activityId={_fx.ActivityId}",
        };

        var adminPages = new[] { "/Organisations", "/Organisations/Create", "/Users", "/Users/Create" };

        var allErrors = new List<string>();

        await using (var coord = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId))
        {
            foreach (var p in coordinatorPages)
                allErrors.AddRange(await CollectErrorsAsync(coord, p));
        }

        await using (var admin = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId))
        {
            foreach (var p in adminPages)
                allErrors.AddRange(await CollectErrorsAsync(admin, p));
        }

        allErrors.Should().BeEmpty(
            "no authenticated view should log a console error / uncaught JS exception:\n" +
            string.Join("\n", allErrors));
    }
}
