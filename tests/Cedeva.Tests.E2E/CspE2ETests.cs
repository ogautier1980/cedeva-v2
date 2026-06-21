using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Verifies the Content-Security-Policy in a real browser on the admin pages that pull the heaviest
/// client libraries (Summernote rich editor, Choices.js selects, jQuery UI autocomplete). The public
/// iframe is covered by <see cref="RegistrationIframeTests"/>; this closes the gap for the editor/
/// widget pages, where a too-strict CSP would silently block a CDN script and break the UI without
/// any server-side error. A CSP refusal surfaces as a console message containing "Content Security
/// Policy", so we fail if any appears.
/// </summary>
[Collection("E2E")]
public class CspE2ETests
{
    private readonly PlaywrightFixture _fx;

    public CspE2ETests(PlaywrightFixture fx) => _fx = fx;

    [Fact]
    public async Task RichClientAdminPages_TriggerNoCspViolations()
    {
        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);

        var pages = new[]
        {
            "/EmailTemplates/Create",                 // Summernote editor
            "/Bookings/Create",                       // Choices.js selects + inline AJAX widgets
            $"/Excursions/Create/{_fx.ActivityId}",   // Choices.js + group checkboxes
        };

        foreach (var path in pages)
        {
            var page = await ctx.NewPageAsync();
            var violations = new List<string>();
            page.Console += (_, msg) =>
            {
                if (msg.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))
                    violations.Add($"{path}: {msg.Text}");
            };

            var response = await page.GotoAsync($"{_fx.BaseUrl}{path}");
            response!.Status.Should().Be(200, $"{path} should render for a coordinator");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            violations.Should().BeEmpty($"{path} must not trigger any Content-Security-Policy refusal");
            await page.CloseAsync();
        }
    }
}
