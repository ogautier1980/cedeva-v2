using Cedeva.Core.DTOs.Payments;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cedeva.Website.Features.OnlinePayment;

/// <summary>
/// Anonymous online-payment endpoints for the public registration flow. Provider-agnostic:
/// depends only on <see cref="IPaymentGateway"/> (Stripe today) and <see cref="IBookingPaymentService"/>.
/// </summary>
[AllowAnonymous]
public class OnlinePaymentController : Controller
{
    private const string Currency = "eur";

    private readonly CedevaDbContext _context;
    private readonly IPaymentGateway _gateway;
    private readonly IBookingPaymentService _bookingPaymentService;
    private readonly ILogger<OnlinePaymentController> _logger;

    public OnlinePaymentController(
        CedevaDbContext context,
        IPaymentGateway gateway,
        IBookingPaymentService bookingPaymentService,
        ILogger<OnlinePaymentController> logger)
    {
        _context = context;
        _gateway = gateway;
        _bookingPaymentService = bookingPaymentService;
        _logger = logger;
    }

    // GET: OnlinePayment/Checkout?bookingId=5 — starts a hosted checkout for the amount still due.
    [HttpGet]
    public async Task<IActionResult> Checkout(int bookingId)
    {
        var booking = await _context.Bookings.IgnoreQueryFilters()
            .Include(b => b.Child).ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return NotFound();

        var amountDue = booking.TotalAmount - booking.PaidAmount;
        if (amountDue <= 0)
            return RedirectToAction("Confirmation", "PublicRegistration", new { bookingId });

        var request = new PaymentCheckoutRequest(
            BookingId: booking.Id,
            Amount: amountDue,
            Currency: Currency,
            Description: $"{booking.Activity.Name} — {booking.Child.FirstName} {booking.Child.LastName}",
            CustomerEmail: booking.Child.Parent?.Email,
            SuccessUrl: Url.Action(nameof(Return), "OnlinePayment", new { bookingId }, Request.Scheme)!,
            CancelUrl: Url.Action("Confirmation", "PublicRegistration", new { bookingId }, Request.Scheme)!);

        var result = await _gateway.CreateCheckoutAsync(request);
        _logger.LogInformation("Started {Provider} checkout {Reference} for booking {BookingId} ({Amount})",
            _gateway.ProviderName, result.ProviderReference, booking.Id, amountDue);

        return Redirect(result.CheckoutUrl);
    }

    // GET: OnlinePayment/Return?bookingId=5 — where the provider redirects the payer after paying.
    // The payment is recorded by the webhook; this just returns the user to the confirmation page.
    [HttpGet]
    public IActionResult Return(int bookingId)
        => RedirectToAction("Confirmation", "PublicRegistration", new { bookingId });

    // POST: OnlinePayment/Webhook — server-to-server callback from the provider (signed).
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        var result = _gateway.ParseWebhook(body, signature);
        if (result == null)
            return BadRequest();

        if (result.IsPaid)
            await _bookingPaymentService.ApplySuccessfulPaymentAsync(result);

        return Ok();
    }
}
