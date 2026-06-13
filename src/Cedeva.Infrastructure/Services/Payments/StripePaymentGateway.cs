using Cedeva.Core.DTOs.Payments;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Cedeva.Infrastructure.Services.Payments;

/// <summary>Stripe implementation of <see cref="IPaymentGateway"/> using Stripe Checkout (hosted).</summary>
public class StripePaymentGateway : IPaymentGateway
{
    private const string CheckoutCompletedEvent = "checkout.session.completed";
    private const string BookingIdMetadataKey = "bookingId";

    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentGateway> _logger;

    public string ProviderName => "Stripe";

    public StripePaymentGateway(IOptions<StripeOptions> options, ILogger<StripePaymentGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // Built lazily: StripeClient throws on an empty key, so the gateway stays resolvable even
    // before Stripe is configured (only an actual checkout fails, with a clear message).
    private StripeClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InvalidOperationException("Stripe is not configured (Stripe:SecretKey is empty).");
        return new StripeClient(_options.SecretKey);
    }

    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var sessionOptions = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : request.CustomerEmail,
            ClientReferenceId = request.BookingId.ToString(),
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        UnitAmount = (long)Math.Round(request.Amount * 100m),
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = request.Description }
                    }
                }
            ],
            Metadata = new Dictionary<string, string> { [BookingIdMetadataKey] = request.BookingId.ToString() }
        };

        var session = await new SessionService(CreateClient()).CreateAsync(sessionOptions, cancellationToken: cancellationToken);
        return new PaymentCheckoutResult(session.Url, session.Id);
    }

    public PaymentWebhookResult? ParseWebhook(string requestBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader))
            return null;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(requestBody, signatureHeader, _options.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Rejected Stripe webhook with invalid signature");
            return null;
        }

        if (stripeEvent.Type != CheckoutCompletedEvent || stripeEvent.Data.Object is not Session session)
            return null;

        var isPaid = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
        var amount = (session.AmountTotal ?? 0) / 100m;
        return new PaymentWebhookResult(isPaid, ResolveBookingId(session), session.Id, amount,
            session.Currency ?? _options.Currency);
    }

    private static int ResolveBookingId(Session session)
    {
        if (session.Metadata != null
            && session.Metadata.TryGetValue(BookingIdMetadataKey, out var meta)
            && int.TryParse(meta, out var id))
        {
            return id;
        }
        return int.TryParse(session.ClientReferenceId, out var fromRef) ? fromRef : 0;
    }
}
