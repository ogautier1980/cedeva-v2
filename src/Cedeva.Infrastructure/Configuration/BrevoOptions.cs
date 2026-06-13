namespace Cedeva.Infrastructure.Configuration;

/// <summary>Strongly-typed binding for the "Brevo" configuration section.</summary>
public class BrevoOptions
{
    public const string SectionName = "Brevo";

    public string ApiBaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
}
