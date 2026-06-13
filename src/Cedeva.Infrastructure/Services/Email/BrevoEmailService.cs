using Cedeva.Core.DTOs;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using Task = System.Threading.Tasks.Task; // Resolve conflict with brevo_csharp.Model.Task

namespace Cedeva.Infrastructure.Services.Email;

public class BrevoEmailService : IEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BrevoEmailService> _logger;
    private readonly string _senderEmail;
    private readonly string _senderName;

    public BrevoEmailService(
        IOptions<BrevoOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<BrevoEmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var brevo = options.Value;
        _senderEmail = !string.IsNullOrWhiteSpace(brevo.SenderEmail)
            ? brevo.SenderEmail
            : throw new InvalidOperationException("Brevo sender email not configured");
        _senderName = !string.IsNullOrWhiteSpace(brevo.SenderName)
            ? brevo.SenderName
            : throw new InvalidOperationException("Brevo sender name not configured");
    }

    public async Task SendEmailAsync(string to, string subject, string htmlContent, string? attachmentPath = null)
    {
        await SendEmailAsync(new[] { to }, subject, htmlContent, attachmentPath);
    }

    public async Task SendEmailAsync(IEnumerable<string> to, string subject, string htmlContent, string? attachmentPath = null)
    {
        var cleanRecipients = to
        .Where(addr => !string.IsNullOrWhiteSpace(addr))
        .Select(addr => addr.Trim())
        .ToList();

        if (!cleanRecipients.Any())
        {
            _logger.LogWarning("Sending cancelled: no valid recipient.");
            return;
        }

        object payload;

        if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath))
        {
            var fileBytes = await File.ReadAllBytesAsync(attachmentPath);
            var base64Content = Convert.ToBase64String(fileBytes);
            var fileName = Path.GetFileName(attachmentPath);

            payload = new
            {
                sender = new { name = _senderName, email = _senderEmail },
                to = cleanRecipients.Select(addr => new { email = addr, name = addr }).ToArray(),
                subject = subject,
                htmlContent = htmlContent,
                attachment = new[]
                {
                    new
                    {
                        name = fileName,
                        content = base64Content
                    }
                }
            };

            _logger.LogInformation("Email prepared with attachment: {FileName} ({Size} bytes)", fileName, fileBytes.Length);
        }
        else
        {
            payload = new
            {
                sender = new { name = _senderName, email = _senderEmail },
                to = cleanRecipients.Select(addr => new { email = addr, name = addr }).ToArray(),
                subject = subject,
                htmlContent = htmlContent
            };

            if (!string.IsNullOrWhiteSpace(attachmentPath))
            {
                _logger.LogWarning("Attachment path specified but file not found: {Path}", attachmentPath);
            }
        }

        try
        {
            _logger.LogInformation("Sending email FROM {SenderEmail} TO {Recipients}...", _senderEmail, string.Join(", ", cleanRecipients));

            var client = _httpClientFactory.CreateClient("BrevoClient");
            var response = await client.PostAsJsonAsync("/v3/smtp/email", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Brevo Error ({response.StatusCode}): {errorContent}");
            }

            _logger.LogInformation("Email successfully sent to {Count} recipient(s).", cleanRecipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error sending email via Brevo to {Recipients}", string.Join(", ", cleanRecipients));
            throw new InvalidOperationException($"Failed to send email to {cleanRecipients.Count} recipient(s). See inner exception for details.", ex);
        }
    }

    public async Task SendBookingConfirmationEmailAsync(
        string parentEmail,
        string parentName,
        string childName,
        string activityName,
        DateTime startDate,
        DateTime endDate)
    {
        var subject = $"Confirmation d'inscription - {activityName}";

        var htmlContent = $@"
            <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background-color: #f9f9f9; }}
                        .details {{ background-color: white; padding: 15px; margin: 15px 0; border-left: 4px solid #4CAF50; }}
                        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>Confirmation d'inscription</h1>
                        </div>
                        <div class='content'>
                            <p>Bonjour {parentName},</p>
                            <p>Nous confirmons l'inscription de <strong>{childName}</strong> pour l'activité suivante :</p>
                            <div class='details'>
                                <h3>{activityName}</h3>
                                <p><strong>Date de début :</strong> {startDate:dd/MM/yyyy}</p>
                                <p><strong>Date de fin :</strong> {endDate:dd/MM/yyyy}</p>
                            </div>
                            <p>Nous sommes impatients d'accueillir votre enfant pour cette activité !</p>
                            <p>Si vous avez des questions, n'hésitez pas à nous contacter.</p>
                            <p>Cordialement,<br>L'équipe Cedeva</p>
                        </div>
                        <div class='footer'>
                            <p>Cet email a été envoyé automatiquement par Cedeva.</p>
                        </div>
                    </div>
                </body>
            </html>";

        await SendEmailAsync(parentEmail, subject, htmlContent);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("csharpsquid", "S2325:Methods should be static if they do not reference instance data",
        Justification = "False positive - method calls instance method SendEmailAsync")]
    public async Task SendBookingConfirmationEmailAsync(BookingConfirmationEmailDto data)
    {
        var subject = $"Confirmation d'inscription - {data.ActivityName}";

        var htmlContent = $@"
            <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background-color: #f9f9f9; }}
                        .details {{ background-color: white; padding: 15px; margin: 15px 0; border-left: 4px solid #4CAF50; }}
                        .payment-info {{ background-color: #fff8e1; padding: 15px; margin: 15px 0; border-left: 4px solid #FFC107; }}
                        .highlight {{ font-size: 18px; font-weight: bold; color: #4CAF50; }}
                        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>Confirmation d'inscription</h1>
                        </div>
                        <div class='content'>
                            <p>Bonjour {data.ParentName},</p>
                            <p>Nous confirmons l'inscription de <strong>{data.ChildName}</strong> pour l'activité suivante :</p>
                            <div class='details'>
                                <h3>{data.ActivityName}</h3>
                                <p><strong>Date de début :</strong> {data.StartDate:dd/MM/yyyy}</p>
                                <p><strong>Date de fin :</strong> {data.EndDate:dd/MM/yyyy}</p>
                            </div>
                            <div class='payment-info'>
                                <h3>Informations de paiement</h3>
                                <p class='highlight'>Montant à payer : {data.TotalAmount:F2} €</p>
                                <p><strong>Compte bancaire (IBAN) :</strong><br>{data.BankAccount}</p>
                                <p><strong>Communication structurée (OBLIGATOIRE) :</strong><br><span style='font-size: 16px; font-family: monospace; font-weight: bold;'>{data.StructuredCommunication}</span></p>
                                <p style='color: #d32f2f; font-weight: bold;'>⚠ IMPORTANT : N'oubliez pas d'indiquer la communication structurée lors de votre virement !</p>
                            </div>
                            <p>Nous sommes impatients d'accueillir votre enfant pour cette activité !</p>
                            <p>Si vous avez des questions, n'hésitez pas à nous contacter.</p>
                            <p>Cordialement,<br>L'équipe Cedeva</p>
                        </div>
                        <div class='footer'>
                            <p>Cet email a été envoyé automatiquement par Cedeva.</p>
                        </div>
                    </div>
                </body>
            </html>";

        await SendEmailAsync(data.ParentEmail, subject, htmlContent);
    }

    public async Task SendWelcomeEmailAsync(string userEmail, string userName, string organisationName)
    {
        var subject = "Bienvenue sur Cedeva !";

        var htmlContent = $@"
            <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background-color: #f9f9f9; }}
                        .highlight {{ background-color: white; padding: 15px; margin: 15px 0; border-left: 4px solid #2196F3; }}
                        .button {{ display: inline-block; padding: 12px 24px; background-color: #2196F3; color: white; text-decoration: none; border-radius: 4px; margin: 10px 0; }}
                        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>Bienvenue sur Cedeva !</h1>
                        </div>
                        <div class='content'>
                            <p>Bonjour {userName},</p>
                            <p>Nous sommes ravis de vous accueillir sur la plateforme Cedeva !</p>
                            <div class='highlight'>
                                <h3>Votre compte a été créé avec succès</h3>
                                <p><strong>Organisation :</strong> {organisationName}</p>
                                <p><strong>Email :</strong> {userEmail}</p>
                            </div>
                            <p>Vous pouvez maintenant vous connecter et commencer à utiliser la plateforme pour gérer vos activités et inscriptions.</p>
                            <p>Si vous avez des questions ou besoin d'aide, notre équipe est là pour vous accompagner.</p>
                            <p>Cordialement,<br>L'équipe Cedeva</p>
                        </div>
                        <div class='footer'>
                            <p>Cet email a été envoyé automatiquement par Cedeva.</p>
                        </div>
                    </div>
                </body>
            </html>";

        await SendEmailAsync(userEmail, subject, htmlContent);
    }
}
