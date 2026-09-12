using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>Integration tests for PaymentsController.Edit/Delete (Lot K item #8).</summary>
[Collection("WebApp")]
public class PaymentsControllerEditDeleteTests
{
    private sealed record Scenario(int OrgId, int BookingId, int PaymentId);

    private static Scenario SeedPaidBooking(CedevaWebApplicationFactory factory, decimal total, decimal paidAmount)
    {
        Organisation org = null!;
        Booking booking = null!;
        Payment payment = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group: null, totalAmount: total, paidAmount: paidAmount);
            booking.PaymentStatus = paidAmount >= total ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;
            payment = new Payment
            {
                Booking = booking,
                Amount = paidAmount,
                Status = PaymentStatus.Paid,
                PaymentMethod = PaymentMethod.Cash,
                PaymentDate = new DateTime(2026, 6, 1),
                TicketNumber = 1,
            };
            ctx.AddRange(org, activity, parent, child, booking, payment);
            return 0;
        });
        return new Scenario(org.Id, booking.Id, payment.Id);
    }

    [Fact]
    public async Task Edit_Get_RendersFormWithCurrentValues()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedPaidBooking(factory, total: 100m, paidAmount: 60m);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Payments/Edit/{s.PaymentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("60");
    }

    [Fact]
    public async Task Edit_Post_IncreasesAmount_AdjustsBookingPaidAmountByDelta()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedPaidBooking(factory, total: 100m, paidAmount: 60m);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Payments/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = s.PaymentId.ToString(),
            ["BookingId"] = s.BookingId.ToString(),
            ["Amount"] = "90",
            ["PaymentDate"] = "2026-06-01",
            ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Bookings/Details");

        await using var ctx = factory.NewDbContext();
        var booking = await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.PaidAmount.Should().Be(90m);
        booking.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);
        (await ctx.Payments.SingleAsync(p => p.Id == s.PaymentId)).Amount.Should().Be(90m);
    }

    [Fact]
    public async Task Edit_Post_DecreasesAmount_CanBringBookingBelowFullyPaid()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedPaidBooking(factory, total: 100m, paidAmount: 100m);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Payments/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = s.PaymentId.ToString(),
            ["BookingId"] = s.BookingId.ToString(),
            ["Amount"] = "40",
            ["PaymentDate"] = "2026-06-01",
            ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        var booking = await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.PaidAmount.Should().Be(40m);
        booking.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);
    }

    [Fact]
    public async Task Edit_Get_CancelledPayment_RedirectsWithoutRenderingForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedPaidBooking(factory, total: 100m, paidAmount: 60m);

        await using (var ctx = factory.NewDbContext())
        {
            var payment = await ctx.Payments.SingleAsync(p => p.Id == s.PaymentId);
            payment.Status = PaymentStatus.Cancelled;
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
        var response = await client.GetAsync($"/Payments/Edit/{s.PaymentId}");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Payments/Details");
    }

    [Fact]
    public async Task Delete_RemovesPaymentRow_AndReducesBookingPaidAmount()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedPaidBooking(factory, total: 100m, paidAmount: 60m);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync($"/Payments/Delete/{s.PaymentId}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Bookings/Details");

        await using var ctx = factory.NewDbContext();
        (await ctx.Payments.AnyAsync(p => p.Id == s.PaymentId)).Should().BeFalse();
        var booking = await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.PaidAmount.Should().Be(0m);
        booking.PaymentStatus.Should().Be(PaymentStatus.NotPaid);
    }

    [Fact]
    public async Task Delete_UnknownPayment_RedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/Payments/Delete/999999",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Payments");
    }
}
