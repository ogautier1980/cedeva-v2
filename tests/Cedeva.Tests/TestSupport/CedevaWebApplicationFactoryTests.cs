using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Cedeva.Tests.TestSupport;

[Collection("WebApp")]
public class CedevaWebApplicationFactoryTests
{
    [Fact]
    public void TestHost_DoesNotWatchConfigurationFiles()
    {
        // Guards the CI fix: watched appsettings files cost one inotify instance per test host on
        // Linux, and the suite exhausts the runner's limit (128) — see the factory's static ctor.
        using var factory = new CedevaWebApplicationFactory();
        var configuration = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();

        var jsonSources = configuration.Providers.OfType<JsonConfigurationProvider>().Select(p => p.Source).ToList();

        jsonSources.Should().NotBeEmpty();
        jsonSources.Should().OnlyContain(s => !s.ReloadOnChange);
    }
}
