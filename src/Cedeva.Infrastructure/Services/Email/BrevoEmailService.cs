using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Cedeva.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Services.Email;

public class BrevoEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BrevoEmailService> _logger;
    private readonly string _senderEmail;
    private readonly string _senderName;

    public BrevoEmailService(
        IConfiguration configuration,
        ILogger<BrevoEmailService> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;

        _senderEmail = configuration["Brevo:SenderEmail"]
            ?? throw new InvalidOperationException("Brevo sender email not configured");
        _senderName = configuration["Brevo:SenderName"]
            ?? throw new InvalidOperationException("Brevo sender name not configured");

        _logger.LogInformation("BrevoEmailService initialized with sender: {SenderEmail} ({SenderName})",
            _senderEmail, _senderName);
    }

    public async Task SendEmailAsync(string to, string subject, string htmlContent, string? attachmentPath = null)
    {
        await SendEmailAsync(new[] { to }, subject, htmlContent, attachmentPath);
    }

    public async Task SendEmailAsync(IEnumerable<string> to, string subject, string htmlContent, string? attachmentPath = null)
    {
        var recipients = to.Select(email => new { email }).ToArray();

        var payload = new
        {
            sender = new { email = _senderEmail, name = _senderName },
            to = recipients,
            subject,
            htmlContent
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            _logger.LogInformation("Sending email FROM {SenderEmail} TO {Recipients} with subject '{Subject}'",
                _senderEmail, string.Join(", ", to), subject);
            _logger.LogDebug("Request payload: {Payload}", json);
            _logger.LogDebug("HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            _logger.LogDebug("Full request URL will be: {FullUrl}",
                _httpClient.BaseAddress != null
                    ? new Uri(_httpClient.BaseAddress, "smtp/email").ToString()
                    : "smtp/email (NO BASE ADDRESS SET!)");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.PostAsync("smtp/email", content);
            stopwatch.Stop();
            _logger.LogInformation("Brevo API call completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send email via Brevo. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to send email: {errorContent}");
            }

            _logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", to));
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error sending email via Brevo to {Recipients}", string.Join(", ", to));
            throw new InvalidOperationException($"Error sending email to {string.Join(", ", to)}", ex);
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
        // Version de compatibilité sans informations de paiement
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

    public async Task SendBookingConfirmationEmailAsync(
        string parentEmail,
        string parentName,
        string childName,
        string activityName,
        DateTime startDate,
        DateTime endDate,
        decimal totalAmount,
        string structuredCommunication,
        string bankAccount)
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
                            <p>Bonjour {parentName},</p>
                            <p>Nous confirmons l'inscription de <strong>{childName}</strong> pour l'activité suivante :</p>
                            <div class='details'>
                                <h3>{activityName}</h3>
                                <p><strong>Date de début :</strong> {startDate:dd/MM/yyyy}</p>
                                <p><strong>Date de fin :</strong> {endDate:dd/MM/yyyy}</p>
                            </div>
                            <div class='payment-info'>
                                <h3>Informations de paiement</h3>
                                <p class='highlight'>Montant à payer : {totalAmount:F2} €</p>
                                <p><strong>Compte bancaire (IBAN) :</strong><br>{bankAccount}</p>
                                <p><strong>Communication structurée (OBLIGATOIRE) :</strong><br><span style='font-size: 16px; font-family: monospace; font-weight: bold;'>{structuredCommunication}</span></p>
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

        await SendEmailAsync(parentEmail, subject, htmlContent);
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
