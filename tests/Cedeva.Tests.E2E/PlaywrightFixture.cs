using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Shared across the E2E collection: boots the app on Kestrel once, seeds data, and launches a
/// single headless Chromium browser. Each test opens its own page (anonymous) or an authenticated
/// context via <see cref="NewAuthedContextAsync"/>.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public PlaywrightAppFactory Factory { get; private set; } = null!;
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public int ActivityId => Factory.ActivityId;
    public int OrganisationId => Factory.OrganisationId;
    public string BaseUrl => Factory.ServerAddress.TrimEnd('/');

    public async Task InitializeAsync()
    {
        Factory = new PlaywrightAppFactory();
        Factory.SeedData();
        _ = Factory.ServerAddress; // ensure the Kestrel host is started

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>A browser context whose every request is authenticated as the given role/org.</summary>
    public Task<IBrowserContext> NewAuthedContextAsync(string role, int organisationId) =>
        Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                [TestAuthHandler.UserHeader] = $"e2e-user|{organisationId}|{role}"
            }
        });

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();
        Factory?.Dispose();
    }
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<PlaywrightFixture>;
