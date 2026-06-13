using Cedeva.Core.DTOs.Payments;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Services.Payments;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cedeva.Tests.Services.Payments;

public class BookingPaymentServiceTests
{
    private static (SqliteTestContext db, int bookingId) Seed(decimal total = 100m, decimal paid = 0m)
    {
        var db = new SqliteTestContext();
        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);
        var booking = TestData.Booking(child, activity, group: null, totalAmount: total, paidAmount: paid);
        booking.PaymentStatus = PaymentStatus.NotPaid;
        db.Context.AddRange(org, activity, parent, child, booking);
        db.Context.SaveChanges();
        return (db, booking.Id);
    }

    private static BookingPaymentService Sut(SqliteTestContext db) =>
        new(db.Context, NullLogger<BookingPaymentService>.Instance);

    [Fact]
    public async Task ApplySuccessfulPayment_FullAmount_RecordsPaymentAndMarksPaid()
    {
        var (db, bookingId) = Seed(total: 100m);
        using var _ = db;

        var applied = await Sut(db).ApplySuccessfulPaymentAsync(
            new PaymentWebhookResult(IsPaid: true, BookingId: bookingId, ProviderReference: "evt_1", AmountPaid: 100m, Currency: "eur"));

        applied.Should().BeTrue();

        await using var verify = db.NewContext();
        var booking = await verify.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.PaidAmount.Should().Be(100m);
        booking.PaymentStatus.Should().Be(PaymentStatus.Paid);
        var payment = await verify.Payments.SingleAsync(p => p.BookingId == bookingId);
        payment.Reference.Should().Be("evt_1");
        payment.PaymentMethod.Should().Be(PaymentMethod.Online);
        payment.Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public async Task ApplySuccessfulPayment_PartialAmount_MarksPartiallyPaid()
    {
        var (db, bookingId) = Seed(total: 100m);
        using var _ = db;

        await Sut(db).ApplySuccessfulPaymentAsync(
            new PaymentWebhookResult(true, bookingId, "evt_2", 40m, "eur"));

        await using var verify = db.NewContext();
        var booking = await verify.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.PaidAmount.Should().Be(40m);
        booking.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);
    }

    [Fact]
    public async Task ApplySuccessfulPayment_IsIdempotent_OnProviderReference()
    {
        var (db, bookingId) = Seed(total: 100m);
        using var _ = db;
        var sut = Sut(db);
        var evt = new PaymentWebhookResult(true, bookingId, "evt_dup", 100m, "eur");

        var first = await sut.ApplySuccessfulPaymentAsync(evt);
        var second = await sut.ApplySuccessfulPaymentAsync(evt); // webhook retry

        first.Should().BeTrue();
        second.Should().BeFalse();

        await using var verify = db.NewContext();
        (await verify.Payments.CountAsync(p => p.BookingId == bookingId)).Should().Be(1);
        (await verify.Bookings.SingleAsync(b => b.Id == bookingId)).PaidAmount.Should().Be(100m);
    }

    [Fact]
    public async Task ApplySuccessfulPayment_WhenNotPaid_DoesNothing()
    {
        var (db, bookingId) = Seed();
        using var _ = db;

        var applied = await Sut(db).ApplySuccessfulPaymentAsync(
            new PaymentWebhookResult(IsPaid: false, bookingId, "evt_unpaid", 100m, "eur"));

        applied.Should().BeFalse();
        await using var verify = db.NewContext();
        (await verify.Payments.AnyAsync(p => p.BookingId == bookingId)).Should().BeFalse();
    }

    [Fact]
    public async Task ApplySuccessfulPayment_UnknownBooking_ReturnsFalse()
    {
        var (db, _) = Seed();
        using var __ = db;

        var applied = await Sut(db).ApplySuccessfulPaymentAsync(
            new PaymentWebhookResult(true, BookingId: 999999, "evt_x", 50m, "eur"));

        applied.Should().BeFalse();
    }
}
