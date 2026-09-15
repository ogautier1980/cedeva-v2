using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Services;

public class PaymentLinkEmailService : IPaymentLinkEmailService
{
    private readonly IEmailFacadeService _emailServices;
    private readonly IQrCodeService _qrCodeService;
    private readonly ILogger<PaymentLinkEmailService> _logger;

    public PaymentLinkEmailService(
        IEmailFacadeService emailServices,
        IQrCodeService qrCodeService,
        ILogger<PaymentLinkEmailService> logger)
    {
        _emailServices = emailServices;
        _qrCodeService = qrCodeService;
        _logger = logger;
    }

    public async Task<bool> SendPaymentLinkEmailAsync(Booking booking, Organisation organisation, string checkoutUrl)
    {
        try
        {
            var parentEmail = booking.Child.Parent?.Email;
            if (string.IsNullOrWhiteSpace(parentEmail))
                return false;

            var qrDataUri = _qrCodeService.GenerateDataUri(checkoutUrl);
            var qrImageTag = $"<img src=\"{qrDataUri}\" alt=\"QR paiement\" style=\"width:180px;height:180px;\">";

            var extraVariables = new Dictionary<string, string>
            {
                ["lien_paiement"] = checkoutUrl,
                ["qr_code_paiement"] = qrImageTag
            };

            var sent = await _emailServices.SendBookingTemplateAsync(
                EmailTemplateType.PaymentLinkRequest, organisation.Id, [parentEmail], booking, organisation, extraVariables);

            if (!sent)
            {
                var subject = $"Lien de paiement – {booking.Child.FirstName} {booking.Child.LastName} – {booking.Activity.Name}";
                var body =
                    $"<h2 style=\"color:#007faf;\">Confirmation et paiement de votre inscription</h2>" +
                    $"<p>Chère famille,</p>" +
                    $"<p>Nous vous confirmons que l'inscription de <strong>{booking.Child.FirstName} {booking.Child.LastName}</strong> " +
                    $"à <strong>{booking.Activity.Name}</strong> est validée.</p>" +
                    $"<p><strong>Montant restant à payer :</strong> {(booking.TotalAmount - booking.PaidAmount):F2} €</p>" +
                    $"<p style=\"text-align:center;\"><a href=\"{checkoutUrl}\" style=\"background:#007faf;color:#ffffff;" +
                    $"padding:12px 24px;border-radius:4px;text-decoration:none;display:inline-block;\">Payer en ligne</a></p>" +
                    $"<p style=\"text-align:center;\">Ou scannez ce QR code pour payer par carte ou Bancontact :</p>" +
                    $"<p style=\"text-align:center;\">{qrImageTag}</p>" +
                    $"<p>Cordialement,<br><strong>{organisation.Name}</strong></p>";
                await _emailServices.Email.SendEmailAsync(parentEmail, subject, body);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send payment link email for booking {BookingId}", booking.Id);
            return false;
        }
    }
}
