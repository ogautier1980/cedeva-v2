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
        HttpClient? httpClient = null)
    {
        _logger = logger;

        var apiKey = configuration["Brevo:ApiKey"]
            ?? throw new InvalidOperationException("Brevo API key not configured");

        _senderEmail = configuration["Brevo:SenderEmail"] ?? "noreply@cedeva.be";
        _senderName = configuration["Brevo:SenderName"] ?? "Cedeva";

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri("https://api.brevo.com/v3/");
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
            var response = await _httpClient.PostAsync("smtp/email", content);

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
            _logger.LogError(ex, "Error sending email via Brevo");
            throw;
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
                            <p>Nous confirmons l'inscription de <strong>{childName}</strong> à l'activité suivante :</p>
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
