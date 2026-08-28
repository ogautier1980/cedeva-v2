using Cedeva.Core.DTOs.Payments;
using Cedeva.Infrastructure.Configuration;
using Cedeva.Infrastructure.Services.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cedeva.Tests.Services.Payments;

/// <summary>
/// Unit tests for <see cref="MolliePaymentGateway"/>. These never touch the real Mollie API:
/// CreateCheckoutAsync/ParseWebhookAsync are only exercised on the pre-network guards (empty
/// ApiKey, unparsable webhook body).
/// </summary>
public class MolliePaymentGatewayTests
{
    private static MolliePaymentGateway BuildGateway(string apiKey = "test_dummy", string currency = "eur")
    {
        var options = Options.Create(new MollieOptions { ApiKey = apiKey, Currency = currency });
        var services = new ServiceCollection();
        services.AddHttpClient("MollieClient", client => client.BaseAddress = new Uri("https://api.mollie.com/v2/"));
        var httpClientFactory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        return new MolliePaymentGateway(options, httpClientFactory, NullLogger<MolliePaymentGateway>.Instance);
    }

    private static PaymentCheckoutRequest SampleRequest() => new(
        BookingId: 42,
        Amount: 25.50m,
        Currency: "eur",
        Description: "Stage été",
        CustomerEmail: "paul.parent@test.be",
        SuccessUrl: "https://example.test/success",
        CancelUrl: "https://example.test/cancel",
        WebhookUrl: "https://example.test/webhook");

    [Fact]
    public void ProviderName_IsMollie()
    {
        BuildGateway().ProviderName.Should().Be("Mollie");
    }

    [Fact]
    public async Task CreateCheckoutAsync_EmptyApiKey_ThrowsInvalidOperationException()
    {
        var sut = BuildGateway(apiKey: string.Empty);

        var act = async () => await sut.CreateCheckoutAsync(SampleRequest());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Mollie:ApiKey*");
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhitespaceApiKey_ThrowsInvalidOperationException()
    {
        var sut = BuildGateway(apiKey: "   ");

        var act = async () => await sut.CreateCheckoutAsync(SampleRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ParseWebhookAsync_BodyWithoutId_ReturnsNull()
    {
        var sut = BuildGateway();

        (await sut.ParseWebhookAsync("foo=bar", null)).Should().BeNull();
    }

    [Fact]
    public async Task ParseWebhookAsync_EmptyBody_ReturnsNull()
    {
        var sut = BuildGateway();

        (await sut.ParseWebhookAsync(string.Empty, null)).Should().BeNull();
    }

    [Fact]
    public async Task ParseWebhookAsync_EmptyApiKey_ReturnsNull()
    {
        // A payment id is present, so the gateway proceeds to fetch it — but with no ApiKey
        // configured, CreateClient throws, which the gateway catches and turns into null
        // (a webhook must never surface a 500 just because the provider isn't configured).
        var sut = BuildGateway(apiKey: string.Empty);

        (await sut.ParseWebhookAsync("id=tr_7UhSN1zuXS", null)).Should().BeNull();
    }
}
