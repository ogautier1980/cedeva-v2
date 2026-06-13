using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Shared across the E2E collection: boots the app on Kestrel once, seeds a future activity,
/// and launches a single headless Chromium browser. Each test opens its own page.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public PlaywrightAppFactory Factory { get; private set; } = null!;
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public int ActivityId { get; private set; }

    public string BaseUrl => Factory.ServerAddress.TrimEnd('/');

    public async Task InitializeAsync()
    {
        Factory = new PlaywrightAppFactory();
        ActivityId = Factory.SeedFutureActivity();
        _ = Factory.ServerAddress; // ensure the Kestrel host is started

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();
        Factory?.Dispose();
    }
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<PlaywrightFixture>;
