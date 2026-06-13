using Cedeva.Core.DTOs.Payments;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Services.Payments;

/// <summary>
/// Applies a successful online payment to its booking. Provider-agnostic: it consumes the
/// normalised <see cref="PaymentWebhookResult"/>, not any Stripe/Mollie type. Idempotent on the
/// provider reference so webhook retries don't double-credit a booking.
/// </summary>
public class BookingPaymentService : IBookingPaymentService
{
    private readonly CedevaDbContext _context;
    private readonly ILogger<BookingPaymentService> _logger;

    public BookingPaymentService(CedevaDbContext context, ILogger<BookingPaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> ApplySuccessfulPaymentAsync(PaymentWebhookResult payment, CancellationToken cancellationToken = default)
    {
        if (!payment.IsPaid)
            return false;

        // Idempotency: the provider may deliver a webhook more than once.
        var alreadyApplied = await _context.Payments.IgnoreQueryFilters()
            .AnyAsync(p => p.Reference == payment.ProviderReference, cancellationToken);
        if (alreadyApplied)
        {
            _logger.LogInformation("Online payment {Reference} already applied; ignoring duplicate", payment.ProviderReference);
            return false;
        }

        var booking = await _context.Bookings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == payment.BookingId, cancellationToken);
        if (booking == null)
        {
            _logger.LogWarning("Online payment {Reference} references unknown booking {BookingId}",
                payment.ProviderReference, payment.BookingId);
            return false;
        }

        _context.Payments.Add(new Payment
        {
            BookingId = booking.Id,
            Amount = payment.AmountPaid,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = PaymentMethod.Online,
            Status = PaymentStatus.Paid,
            Reference = payment.ProviderReference,
        });

        booking.PaidAmount += payment.AmountPaid;
        booking.PaymentStatus = CalculatePaymentStatus(booking.PaidAmount, booking.TotalAmount);

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Applied online payment {Reference} of {Amount} to booking {BookingId}",
            payment.ProviderReference, payment.AmountPaid, booking.Id);
        return true;
    }

    private static PaymentStatus CalculatePaymentStatus(decimal paidAmount, decimal totalAmount)
    {
        if (paidAmount <= 0) return PaymentStatus.NotPaid;
        if (paidAmount < totalAmount) return PaymentStatus.PartiallyPaid;
        if (paidAmount == totalAmount) return PaymentStatus.Paid;
        return PaymentStatus.Overpaid;
    }
}
