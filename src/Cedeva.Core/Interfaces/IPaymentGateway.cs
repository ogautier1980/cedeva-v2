using Cedeva.Core.DTOs.Payments;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Provider-agnostic online-payment gateway. Swap the implementation (Stripe today, Mollie/…
/// later) without touching the registration flow or the booking-payment logic.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Identifier of the underlying provider (e.g. "Stripe").</summary>
    string ProviderName { get; }

    /// <summary>Creates a hosted checkout session and returns the URL to redirect the payer to.</summary>
    Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies and parses an incoming webhook. Returns null when the payload is invalid/unsigned
    /// or not a relevant payment event.
    /// </summary>
    PaymentWebhookResult? ParseWebhook(string requestBody, string? signatureHeader);
}
