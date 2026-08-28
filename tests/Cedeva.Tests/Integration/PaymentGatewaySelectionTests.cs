using Cedeva.Core.Interfaces;
using Cedeva.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Locks in the "Payments:Provider" config switch: both gateways are always registered, and
/// IPaymentGateway resolves to whichever one the config key names — a config change alone must be
/// enough to move between Stripe and Mollie, no code change. Mollie is the default/fallback;
/// only an explicit "Stripe" (any casing) resolves to Stripe.
/// </summary>
[Collection("WebApp")]
public class PaymentGatewaySelectionTests
{
    [Fact]
    public void DefaultConfig_ResolvesMollieGateway()
    {
        using var factory = new CedevaWebApplicationFactory();
        var gateway = factory.Services.GetRequiredService<IPaymentGateway>();

        gateway.ProviderName.Should().Be("Mollie");
    }

    [Fact]
    public void MollieProviderConfigured_ResolvesMollieGateway()
    {
        using var baseFactory = new CedevaWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Payments:Provider", "Mollie"));
        var gateway = factory.Services.GetRequiredService<IPaymentGateway>();

        gateway.ProviderName.Should().Be("Mollie");
    }

    [Theory]
    [InlineData("Stripe")]
    [InlineData("stripe")]
    [InlineData("STRIPE")]
    public void StripeProviderConfigured_ResolvesStripeGateway(string providerValue)
    {
        using var baseFactory = new CedevaWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Payments:Provider", providerValue));
        var gateway = factory.Services.GetRequiredService<IPaymentGateway>();

        gateway.ProviderName.Should().Be("Stripe");
    }

    [Theory]
    [InlineData("SomeUnknownProvider")]
    [InlineData("")]
    public void UnknownOrEmptyProvider_FallsBackToMollieDefault(string providerValue)
    {
        using var baseFactory = new CedevaWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Payments:Provider", providerValue));
        var gateway = factory.Services.GetRequiredService<IPaymentGateway>();

        gateway.ProviderName.Should().Be("Mollie");
    }
}
