using Cedeva.Core.Entities;

namespace Cedeva.Core.Interfaces;

public interface IPaymentLinkEmailService
{
    /// <summary>
    /// Sends the parent a payment link (+ QR code) for the booking's remaining balance, via the
    /// organisation's PaymentLinkRequest template (falling back to a hard-coded HTML body when no
    /// template is configured). Best-effort: logs and swallows failures instead of throwing, so it
    /// never blocks the caller's booking-confirmation flow.
    /// </summary>
    /// <param name="booking">Must have Child.Parent and Activity loaded.</param>
    /// <param name="checkoutUrl">The Mollie/Stripe checkout URL to embed as a link and as a QR code.</param>
    /// <returns>True when the email was actually sent, so the caller can report it to the user.</returns>
    Task<bool> SendPaymentLinkEmailAsync(Booking booking, Organisation organisation, string checkoutUrl);
}
