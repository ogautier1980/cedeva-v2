using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Additional coverage for <c>PaymentsController</c> targeting actions and branches not
/// exercised by <see cref="PaymentsControllerIntegrationTests"/>: Index filters/listing,
/// SelectBooking, Create GET (+NotFound), Create POST invalid/booking-not-found,
/// Details GET (+NotFound), Cancel not-found and partial-revert branches, Admin role,
/// and tenant scoping behaviour.
/// </summary>
[Collection("WebApp")]
public class PaymentsControllerMoreTests
{
    private sealed record Seeded(
        int OrgId, int ActivityId, int BookingId, int PaymentId, string ActivityName);

    /// <summary>
    /// Seeds one org + activity + parent + child + booking, optionally with a Paid payment.
    /// </summary>
    private static Seeded SeedGraph(
        CedevaWebApplicationFactory factory,
        decimal total,
        decimal paid,
        PaymentStatus bookingStatus,
        bool withPaidPayment = false,
        string activityName = "Stage Test")
    {
        Organisation org = null!;
        Activity activity = null!;
        Booking booking = null!;
        Payment? payment = null;

        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, activityName);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group: null, totalAmount: total, paidAmount: paid);
            booking.PaymentStatus = bookingStatus;
            ctx.AddRange(org, activity, parent, child, booking);

            if (withPaidPayment)
            {
                payment = new Payment
                {
                    Booking = booking,
                    Amount = paid,
                    Status = PaymentStatus.Paid,
                    PaymentMethod = PaymentMethod.Cash,
                    PaymentDate = new DateTime(2026, 6, 1),
                };
                ctx.Add(payment);
            }
            return 0;
        });

        return new Seeded(org.Id, activity.Id, booking.Id, payment?.Id ?? 0, activityName);
    }

    private static HttpClient Unauthenticated(CedevaWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // ---------------------------------------------------------------- Index

    [Fact]
    public async Task Index_ListsExistingPaymentRows()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 100m, PaymentStatus.Paid, withPaidPayment: true);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Payments");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        // Child first name "Chloé" is HTML-encoded in views; assert ASCII portions.
        html.Should().Contain("Enfant");
        html.Should().Contain("Parent");
        html.Should().Contain(s.ActivityName);
    }

    [Fact]
    public async Task Index_FilteredByActivityId_RendersActivityName()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 100m, PaymentStatus.Paid,
            withPaidPayment: true, activityName: "CampEteUnique"); // no apostrophe (HTML-encoded otherwise)
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Payments?activityId={s.ActivityId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("CampEteUnique");
    }

    [Fact]
    public async Task Index_FilteredByBookingId_Succeeds()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 100m, PaymentStatus.Paid, withPaidPayment: true);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Payments?bookingId={s.BookingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Enfant");
    }

    [Fact]
    public async Task Index_ForDifferentOrganisation_ShowsNoPayments()
    {
        using var factory = new CedevaWebApplicationFactory();
        // Unique activity name as the row marker ("Enfant" collides with the "Enfants" nav menu).
        var s = SeedGraph(factory, total: 100m, paid: 100m, PaymentStatus.Paid, withPaidPayment: true,
            activityName: "ZorgActIndexMarker");

        // Index filters on Booking.Activity.OrganisationId, so a foreign org sees nothing.
        var client = factory.CreateClientFor("intruder", s.OrgId + 999, "Coordinator");

        var response = await client.GetAsync("/Payments");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("ZorgActIndexMarker");
    }

    [Fact]
    public async Task Index_AsAdmin_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 50m, paid: 0m, PaymentStatus.NotPaid);
        // Admin has no org claim; Index uses CurrentUserService.OrganisationId (null) -> no rows, still 200.
        var client = factory.CreateClientFor("admin", null, "Admin");

        var response = await client.GetAsync("/Payments");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------- SelectBooking

    [Fact]
    public async Task SelectBooking_ListsBookingsWithOutstandingPayment()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 20m, PaymentStatus.PartiallyPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Payments/SelectBooking");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Enfant");
    }

    [Fact]
    public async Task SelectBooking_WhenAllBookingsFullyPaid_ShowsEmptyState()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 100m, PaymentStatus.Paid,
            activityName: "ZpaidSelMarker"); // unique row marker ("Enfant" collides with the nav menu)
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Payments/SelectBooking");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // A fully-paid booking is excluded from the outstanding list.
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("ZpaidSelMarker");
    }

    [Fact]
    public async Task SelectBooking_ForDifferentOrganisation_ShowsNothing()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid,
            activityName: "ZforeignSelMarker"); // unique row marker ("Enfant" collides with the nav menu)
        var client = factory.CreateClientFor("intruder", s.OrgId + 999, "Coordinator");

        var response = await client.GetAsync("/Payments/SelectBooking");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("ZforeignSelMarker");
    }

    // --------------------------------------------------------------- Create GET

    [Fact]
    public async Task Create_Get_ForExistingBooking_PrefillsRemainingAmount()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 30m, PaymentStatus.PartiallyPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Payments/Create?bookingId={s.BookingId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Enfant");
        // Default Amount = remaining (70.00) bound into the input value.
        html.Should().Contain("70");
    }

    [Fact]
    public async Task Create_Get_ForUnknownBooking_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Payments/Create?bookingId={s.BookingId + 999}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------- Create POST

    [Fact]
    public async Task Create_Post_PartialPayment_SetsPartiallyPaidStatus()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["BookingId"] = s.BookingId.ToString(),
                ["Amount"] = "40",
                ["PaymentDate"] = "2026-07-01",
                ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Bookings/Details");

        await using var ctx = factory.NewDbContext();
        var booking = await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.PaidAmount.Should().Be(40m);
        booking.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);
    }

    [Fact]
    public async Task Create_Post_Overpayment_SetsOverpaidStatus()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["BookingId"] = s.BookingId.ToString(),
                ["Amount"] = "150",
                ["PaymentDate"] = "2026-07-01",
                ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        var booking = await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.PaidAmount.Should().Be(150m);
        booking.PaymentStatus.Should().Be(PaymentStatus.Overpaid);
    }

    [Fact]
    public async Task Create_Post_WithReference_PersistsReference()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["BookingId"] = s.BookingId.ToString(),
                ["Amount"] = "100",
                ["PaymentDate"] = "2026-07-01",
                ["PaymentMethod"] = ((int)PaymentMethod.Other).ToString(),
                ["Reference"] = "INV-2026-42",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        var payment = await ctx.Payments.SingleAsync(p => p.BookingId == s.BookingId);
        payment.Reference.Should().Be("INV-2026-42");
        payment.PaymentMethod.Should().Be(PaymentMethod.Other);
    }

    [Fact]
    public async Task Create_Post_InvalidAmount_ReRendersForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        // Amount 0 violates [Range(0.01, ...)] -> ModelState invalid -> 200 re-render.
        var response = await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["BookingId"] = s.BookingId.ToString(),
                ["Amount"] = "0",
                ["PaymentDate"] = "2026-07-01",
                ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.Payments.AnyAsync(p => p.BookingId == s.BookingId)).Should().BeFalse();
        var booking = await ctx.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.PaidAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Create_Post_UnknownBooking_RedirectsToIndexWithoutCreating()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var missingBookingId = s.BookingId + 999;
        var response = await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["BookingId"] = missingBookingId.ToString(),
                ["Amount"] = "50",
                ["PaymentDate"] = "2026-07-01",
                ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
            }));

        // Booking not found -> redirect to Payments Index (no "Index" segment in URL).
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/Payments");

        await using var ctx = factory.NewDbContext();
        (await ctx.Payments.AnyAsync(p => p.BookingId == missingBookingId)).Should().BeFalse();
    }

    [Fact]
    public async Task Create_Post_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid);
        var client = Unauthenticated(factory);

        var response = await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["BookingId"] = s.BookingId.ToString(),
                ["Amount"] = "10",
                ["PaymentDate"] = "2026-07-01",
                ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------- Details

    [Fact]
    public async Task Details_ForExistingPayment_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 100m, PaymentStatus.Paid, withPaidPayment: true);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Payments/Details/{s.PaymentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Enfant");
        html.Should().Contain(s.ActivityName);
    }

    [Fact]
    public async Task Details_ForUnknownPayment_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Payments/Details/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 100m, PaymentStatus.Paid, withPaidPayment: true);
        var client = Unauthenticated(factory);

        var response = await client.GetAsync($"/Payments/Details/{s.PaymentId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ----------------------------------------------------------------- Cancel

    [Fact]
    public async Task Cancel_UnknownPayment_RedirectsToIndexWithoutChange()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 0m, PaymentStatus.NotPaid);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Payments/Cancel/99999",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/Payments");
    }

    [Fact]
    public async Task Cancel_PartialPayment_RevertsToPartiallyPaid()
    {
        using var factory = new CedevaWebApplicationFactory();
        // Booking paid 100/200 by a single 100 payment; cancelling a 60 payment is not realistic,
        // so seed a booking already at 160/200 and cancel a 60 payment to land at 100 (PartiallyPaid).
        Organisation org = null!;
        Booking booking = null!;
        Payment payment = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group: null, totalAmount: 200m, paidAmount: 160m);
            booking.PaymentStatus = PaymentStatus.PartiallyPaid;
            payment = new Payment
            {
                Booking = booking,
                Amount = 60m,
                Status = PaymentStatus.Paid,
                PaymentMethod = PaymentMethod.Cash,
                PaymentDate = new DateTime(2026, 6, 5),
            };
            ctx.AddRange(org, activity, parent, child, booking, payment);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync($"/Payments/Cancel/{payment.Id}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Bookings/Details");

        await using var ctx2 = factory.NewDbContext();
        var updated = await ctx2.Bookings.SingleAsync(b => b.Id == booking.Id);
        updated.PaidAmount.Should().Be(100m);
        updated.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);
        (await ctx2.Payments.SingleAsync(p => p.Id == payment.Id)).Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedGraph(factory, total: 100m, paid: 100m, PaymentStatus.Paid, withPaidPayment: true);
        var client = Unauthenticated(factory);

        var response = await client.PostAsync($"/Payments/Cancel/{s.PaymentId}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
