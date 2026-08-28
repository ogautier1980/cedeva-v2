using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cedeva.Core.DTOs.Payments;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cedeva.Infrastructure.Services.Payments;

/// <summary>
/// Mollie implementation of <see cref="IPaymentGateway"/> using the Mollie Payments API v2
/// directly over HTTP (no third-party SDK — the API surface used here is small and stable).
/// Mollie doesn't sign its webhook payload: it POSTs the payment id as form data, and the
/// documented way to trust the notification is to fetch that payment back from the API with the
/// secret key, which is what <see cref="ParseWebhookAsync"/> does.
/// </summary>
public class MolliePaymentGateway : IPaymentGateway
{
    private const string HttpClientName = "MollieClient";

    private readonly MollieOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MolliePaymentGateway> _logger;

    public string ProviderName => "Mollie";

    public MolliePaymentGateway(IOptions<MollieOptions> options, IHttpClientFactory httpClientFactory, ILogger<MolliePaymentGateway> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // Built lazily so the gateway stays resolvable even before Mollie is configured (only an
    // actual call fails, with a clear message) — mirrors StripePaymentGateway.CreateClient.
    private HttpClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Mollie is not configured (Mollie:ApiKey is empty).");

        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return client;
    }

    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            amount = new
            {
                currency = request.Currency.ToUpperInvariant(),
                value = request.Amount.ToString("F2", CultureInfo.InvariantCulture)
            },
            description = request.Description,
            redirectUrl = request.SuccessUrl,
            webhookUrl = request.WebhookUrl,
            metadata = new { bookingId = request.BookingId.ToString() }
        };

        using var response = await CreateClient().PostAsJsonAsync("payments", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var payment = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = payment.RootElement;

        var id = root.GetProperty("id").GetString()!;
        var checkoutUrl = root.GetProperty("_links").GetProperty("checkout").GetProperty("href").GetString()!;
        return new PaymentCheckoutResult(checkoutUrl, id);
    }

    public async Task<PaymentWebhookResult?> ParseWebhookAsync(string requestBody, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        // signatureHeader is unused: Mollie has none — trust comes from fetching the payment back
        // from the API with our secret key, not from a header on this request.
        var paymentId = ParseFormPaymentId(requestBody);
        if (string.IsNullOrEmpty(paymentId))
            return null;

        HttpResponseMessage response;
        try
        {
            response = await CreateClient().GetAsync($"payments/{paymentId}", cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot verify Mollie webhook: Mollie is not configured");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Mollie payment lookup for {PaymentId} failed with {StatusCode}", paymentId, response.StatusCode);
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var payment = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = payment.RootElement;

        var status = root.GetProperty("status").GetString();
        var isPaid = string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase);

        var amountElement = root.GetProperty("amount");
        var amount = decimal.Parse(amountElement.GetProperty("value").GetString()!, CultureInfo.InvariantCulture);
        var currency = amountElement.GetProperty("currency").GetString() ?? _options.Currency;

        var bookingId = 0;
        if (root.TryGetProperty("metadata", out var metadata)
            && metadata.ValueKind == JsonValueKind.Object
            && metadata.TryGetProperty("bookingId", out var bookingIdElement)
            && int.TryParse(bookingIdElement.GetString(), out var parsedBookingId))
        {
            bookingId = parsedBookingId;
        }

        return new PaymentWebhookResult(isPaid, bookingId, paymentId, amount, currency);
    }

    private static string? ParseFormPaymentId(string requestBody)
    {
        // Mollie posts "id=tr_xxxxx" as application/x-www-form-urlencoded — no framework model
        // binding here (this is the raw request body), so parse the single field by hand.
        foreach (var pair in requestBody.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == "id")
                return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }
}
