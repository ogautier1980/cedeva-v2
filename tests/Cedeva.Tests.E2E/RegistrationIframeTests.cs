using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser-level smoke tests for the embeddable public-registration iframe. These cover the
/// class of regressions that HTTP-only integration tests cannot see (CSP blocking scripts,
/// JavaScript not running), which is exactly what broke when the CSP omitted cdnjs.
/// </summary>
[Collection("E2E")]
public class RegistrationIframeTests
{
    private readonly PlaywrightFixture _fx;

    public RegistrationIframeTests(PlaywrightFixture fx) => _fx = fx;

    [Fact]
    public async Task RegisterPage_LoadsAndRunsScripts_NotBlockedByCsp()
    {
        var page = await _fx.Browser.NewPageAsync();

        var cspViolations = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))
                cspViolations.Add(msg.Text);
        };

        var url = $"{_fx.BaseUrl}/PublicRegistration/Register?activityId={_fx.ActivityId}";
        var response = await page.GotoAsync(url);

        response!.Status.Should().Be(200);

        // jQuery is loaded from cdnjs — the CSP regression silently blocked it. If the CSP is
        // wrong again, window.jQuery is undefined and this fails (whereas a WAF test would pass).
        var hasJQuery = await page.EvaluateAsync<bool>("() => typeof window.jQuery === 'function'");
        hasJQuery.Should().BeTrue("jQuery from cdnjs must not be blocked by the Content-Security-Policy");

        (await page.Locator("button[type=submit]").IsVisibleAsync())
            .Should().BeTrue("the registration form should render");

        cspViolations.Should().BeEmpty("no script/style should be refused by the Content-Security-Policy");
    }
}
