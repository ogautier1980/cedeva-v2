using Cedeva.Core.DTOs.Payments;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Applies a successful online payment to its booking: records a Payment and updates the booking's
/// PaidAmount / PaymentStatus. Idempotent on the provider reference (safe for webhook retries).
/// </summary>
public interface IBookingPaymentService
{
    /// <summary>Returns true if a new payment was applied, false if ignored (not paid / duplicate / unknown booking).</summary>
    Task<bool> ApplySuccessfulPaymentAsync(PaymentWebhookResult payment, CancellationToken cancellationToken = default);
}
