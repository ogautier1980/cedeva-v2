namespace Cedeva.Infrastructure.Configuration;

/// <summary>Strongly-typed binding for the "Payments" configuration section — selects which
/// <see cref="Cedeva.Core.Interfaces.IPaymentGateway"/> implementation is active.</summary>
public class PaymentsOptions
{
    public const string SectionName = "Payments";

    public const string StripeProvider = "Stripe";
    public const string MollieProvider = "Mollie";

    /// <summary>"Mollie" (default) or "Stripe".</summary>
    public string Provider { get; set; } = MollieProvider;
}
