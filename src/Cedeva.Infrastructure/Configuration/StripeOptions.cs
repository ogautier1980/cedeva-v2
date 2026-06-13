namespace Cedeva.Infrastructure.Configuration;

/// <summary>Strongly-typed binding for the "Stripe" configuration section.</summary>
public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = "eur";
}
