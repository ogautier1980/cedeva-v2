namespace Cedeva.Core.DTOs.Payments;

/// <summary>Input to start a hosted checkout for a booking, independent of the provider.</summary>
public record PaymentCheckoutRequest(
    int BookingId,
    decimal Amount,
    string Currency,
    string Description,
    string? CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    // Only used by providers that require a per-payment callback URL (e.g. Mollie); Stripe ignores
    // it since its webhook endpoint is configured once in the Stripe dashboard.
    string? WebhookUrl = null);

/// <summary>Result of creating a checkout session: where to send the payer + a provider reference.</summary>
public record PaymentCheckoutResult(string CheckoutUrl, string ProviderReference);

/// <summary>Normalised outcome parsed from a provider webhook.</summary>
public record PaymentWebhookResult(
    bool IsPaid,
    int BookingId,
    string ProviderReference,
    decimal AmountPaid,
    string Currency);
