using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class PaymentsControllerIntegrationTests
{
    private sealed record Scenario(int OrgId, int BookingId);

    private static Scenario SeedBooking(CedevaWebApplicationFactory factory,
        decimal total, decimal paid, PaymentStatus status, bool withPaidPayment = false)
    {
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
            if (withPaidPayment)
            {
                ctx.Add(new Payment
                {
                    Booking = booking,
                    Amount = paid,
                    Status = PaymentStatus.Paid,
                    PaymentMethod = PaymentMethod.Cash,
                    PaymentDate = new DateTime(2026, 6, 1),
                });
            }
            return 0;
        });
        return new Scenario(org.Id, booking.Id);
    }

    [Fact]
    public async Task Index_AsCoordinator_RendersPaymentsPage()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.GetAsync("/Payments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Payments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_RecordsPaymentAndMarksBookingPaid()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedBooking(factory, total: 100m, paid: 0m, status: PaymentStatus.NotPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["BookingId"] = s.BookingId.ToString(),
            ["Amount"] = "100",
            ["PaymentDate"] = "2026-07-01",
            ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Bookings/Details");

        await using var ctx = factory.NewDbContext();
        var booking = await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.PaidAmount.Should().Be(100m);
        booking.PaymentStatus.Should().Be(PaymentStatus.Paid);
        (await ctx.Payments.AnyAsync(p => p.BookingId == s.BookingId && p.Status == PaymentStatus.Paid && p.Amount == 100m))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_RevertsBookingPaymentAndMarksPaymentCancelled()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedBooking(factory, total: 100m, paid: 100m, status: PaymentStatus.Paid, withPaidPayment: true);

        int paymentId;
        await using (var seedCtx = factory.NewDbContext())
        {
            paymentId = (await seedCtx.Payments.SingleAsync(p => p.BookingId == s.BookingId)).Id;
        }

        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
        var response = await client.PostAsync($"/Payments/Cancel/{paymentId}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var verify = factory.NewDbContext();
        var booking = await verify.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.PaidAmount.Should().Be(0m);
        booking.PaymentStatus.Should().Be(PaymentStatus.NotPaid);
        (await verify.Payments.SingleAsync(p => p.Id == paymentId)).Status.Should().Be(PaymentStatus.Cancelled);
    }
}
