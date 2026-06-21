using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Drives the defensive catch branches of <c>PaymentsController.Create</c> and <c>Cancel</c> by
/// forcing persistence to fail (<see cref="ThrowingSaveChangesInterceptor"/>). Both wrap their
/// SaveChanges in catch(InvalidOperationException)/catch(DbUpdateException)/catch(Exception); each
/// must redirect (never 500) and leave the data unchanged.
/// </summary>
[Collection("WebApp")]
public class PaymentsErrorPathTests
{
    private static (CedevaWebApplicationFactory factory, int orgId, int bookingId) SeedBooking(decimal total, decimal paid, PaymentStatus status)
    {
        var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Booking booking = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group: null, totalAmount: total, paidAmount: paid);
            booking.PaymentStatus = status;
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });
        return (factory, org.Id, booking.Id);
    }

    [Theory]
    [MemberData(nameof(SaveFailures.Kinds), MemberType = typeof(SaveFailures))]
    public async Task Create_WhenSaveFails_RedirectsWithoutPersisting(string kind)
    {
        var (factory, orgId, bookingId) = SeedBooking(total: 100m, paid: 0m, status: PaymentStatus.NotPaid);
        using (factory)
        {
            var client = factory.CreateClientFor("u1", orgId, "Coordinator");
            factory.ThrowOnSaveChanges = SaveFailures.Make(kind);

            var response = await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["BookingId"] = bookingId.ToString(),
                ["Amount"] = "100",
                ["PaymentDate"] = "2026-07-01",
                ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
            }));

            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Found);

            factory.ThrowOnSaveChanges = null;
            using var db = factory.NewDbContext();
            (await db.Payments.AnyAsync(p => p.BookingId == bookingId))
                .Should().BeFalse("a failed payment must not be recorded");
        }
    }

    [Theory]
    [MemberData(nameof(SaveFailures.Kinds), MemberType = typeof(SaveFailures))]
    public async Task Cancel_WhenSaveFails_RedirectsWithoutCancelling(string kind)
    {
        var (factory, orgId, bookingId) = SeedBooking(total: 100m, paid: 100m, status: PaymentStatus.Paid);
        int paymentId = factory.Seed(ctx =>
        {
            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = 100m,
                Status = PaymentStatus.Paid,
                PaymentMethod = PaymentMethod.Cash,
                PaymentDate = new DateTime(2026, 6, 1),
            };
            ctx.Payments.Add(payment);
            ctx.SaveChanges();
            return payment.Id;
        });

        using (factory)
        {
            var client = factory.CreateClientFor("u1", orgId, "Coordinator");
            factory.ThrowOnSaveChanges = SaveFailures.Make(kind);

            var response = await client.PostAsync($"/Payments/Cancel/{paymentId}",
                new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Found);

            factory.ThrowOnSaveChanges = null;
            using var db = factory.NewDbContext();
            (await db.Payments.SingleAsync(p => p.Id == paymentId)).Status
                .Should().Be(PaymentStatus.Paid, "a failed cancel must not change the payment status");
        }
    }
}
