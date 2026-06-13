using Cedeva.Core.DTOs.Payments;
using Cedeva.Infrastructure.Configuration;
using Cedeva.Infrastructure.Services.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cedeva.Tests.Services.Payments;

/// <summary>
/// Unit tests for <see cref="StripePaymentGateway"/>. These never touch the real Stripe API:
/// CreateCheckoutAsync is only exercised on the pre-network guard (empty SecretKey), and
/// ParseWebhook is exercised on the local guards that run before any network call
/// (missing signature header, signature verification failure).
/// </summary>
public class StripePaymentGatewayTests
{
    private static StripePaymentGateway BuildGateway(
        string secretKey = "sk_test_dummy",
        string webhookSecret = "whsec_dummy",
        string currency = "eur")
    {
        var options = Options.Create(new StripeOptions
        {
            SecretKey = secretKey,
            WebhookSecret = webhookSecret,
            Currency = currency
        });
        return new StripePaymentGateway(options, NullLogger<StripePaymentGateway>.Instance);
    }

    private static PaymentCheckoutRequest SampleRequest() => new(
        BookingId: 42,
        Amount: 25.50m,
        Currency: "eur",
        Description: "Stage été",
        CustomerEmail: "paul.parent@test.be",
        SuccessUrl: "https://example.test/success",
        CancelUrl: "https://example.test/cancel");

    [Fact]
    public void ProviderName_IsStripe()
    {
        BuildGateway().ProviderName.Should().Be("Stripe");
    }

    [Fact]
    public void ParseWebhook_NullSignatureHeader_ReturnsNull()
    {
        var sut = BuildGateway();

        sut.ParseWebhook("{}", null).Should().BeNull();
    }

    [Fact]
    public void ParseWebhook_EmptySignatureHeader_ReturnsNull()
    {
        var sut = BuildGateway();

        sut.ParseWebhook("{}", string.Empty).Should().BeNull();
    }

    [Fact]
    public void ParseWebhook_InvalidSignature_ReturnsNull()
    {
        // A non-empty but bogus signature header makes Stripe's EventUtility.ConstructEvent
        // throw a StripeException, which the gateway catches and converts to null.
        var sut = BuildGateway();

        sut.ParseWebhook("{\"id\":\"evt_1\"}", "t=1,v1=deadbeef").Should().BeNull();
    }

    [Fact]
    public async Task CreateCheckoutAsync_EmptySecretKey_ThrowsInvalidOperationException()
    {
        var sut = BuildGateway(secretKey: string.Empty);

        var act = async () => await sut.CreateCheckoutAsync(SampleRequest());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Stripe:SecretKey*");
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhitespaceSecretKey_ThrowsInvalidOperationException()
    {
        var sut = BuildGateway(secretKey: "   ");

        var act = async () => await sut.CreateCheckoutAsync(SampleRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
