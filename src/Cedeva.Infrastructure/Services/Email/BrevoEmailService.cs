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
}
