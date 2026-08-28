namespace Cedeva.Infrastructure.Configuration;

/// <summary>Strongly-typed binding for the "Mollie" configuration section.</summary>
public class MollieOptions
{
    public const string SectionName = "Mollie";

    public string ApiKey { get; set; } = string.Empty;
    public string Currency { get; set; } = "eur";
}
