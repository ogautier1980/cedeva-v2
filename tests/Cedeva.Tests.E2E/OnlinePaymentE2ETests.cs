using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser coverage of the public online-payment entry points (Stripe), driven anonymously like a
/// real parent landing on the post-registration confirmation page. These tests deliberately do NOT
/// reach Stripe's hosted checkout (no API key in CI): they assert the app-side behaviour that does
/// not need the provider — the "Pay online" button only shows when there is a balance due, and the
/// Checkout endpoint's amount-due guard redirects a fully-paid booking straight back to the
/// confirmation page (no charge). Together they prove the public booking carries a real amount
/// (TotalAmount &gt; 0) and that the payment is offered for it.
/// </summary>
[Collection("E2E")]
public class OnlinePaymentE2ETests
{
    private readonly PlaywrightFixture _fx;

    public OnlinePaymentE2ETests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>Seeds a confirmed booking (with parent/child/activity) under the fixture org and
    /// returns its id. <paramref name="paidInFull"/> controls whether a balance remains.</summary>
    private int SeedBooking(bool paidInFull)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        return _fx.Factory.Seed(ctx =>
        {
            var activity = new Activity
            {
                Name = $"PayStage-{tag}",
                Description = "Online payment E2E",
                IsActive = true,
                PricePerDay = 15m,
                StartDate = DateTime.Today.AddMonths(2),
                EndDate = DateTime.Today.AddMonths(2).AddDays(4),
                OrganisationId = _fx.OrganisationId
            };
            var parent = new Parent
            {
                FirstName = "Paul",
                LastName = $"Payer-{tag}",
                Email = $"payer-{tag}@test.be",
                MobilePhoneNumber = "0470000000",
                NationalRegisterNumber = "85061513380",
                OrganisationId = _fx.OrganisationId,
                Address = new Address { Street = "Rue Test 1", City = "Bruxelles", PostalCode = "1000", Country = Country.Belgium }
            };
            var child = new Child
            {
                FirstName = "Lucas",
                LastName = $"Payer-{tag}",
                BirthDate = new DateTime(2016, 7, 8),
                NationalRegisterNumber = "16070816410",
                Parent = parent
            };
            ctx.Activities.Add(activity);
            ctx.Children.Add(child);
            ctx.SaveChanges();

            var booking = new Booking
            {
                BookingDate = DateTime.Today,
                ChildId = child.Id,
                ActivityId = activity.Id,
                IsConfirmed = true,
                IsMedicalSheet = false,
                TotalAmount = 30m,
                PaidAmount = paidInFull ? 30m : 0m,
                PaymentStatus = paidInFull ? PaymentStatus.Paid : PaymentStatus.NotPaid
            };
            ctx.Bookings.Add(booking);
            ctx.SaveChanges();
            return booking.Id;
        });
    }

    [Fact]
    public async Task Confirmation_WithBalanceDue_ShowsPayOnlineButton()
    {
        var bookingId = SeedBooking(paidInFull: false);

        // Anonymous visitor (no auth header), exactly like a parent after registering via the iframe.
        var page = await _fx.Browser.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/PublicRegistration/Confirmation?bookingId={bookingId}");
        response!.Status.Should().Be(200);

        // The "Pay online" call-to-action links to the Checkout endpoint for this booking.
        var payLink = page.Locator($"a[href*='/OnlinePayment/Checkout'][href*='bookingId={bookingId}']");
        (await payLink.CountAsync()).Should().Be(1, "a booking with a balance due must offer online payment");
        (await payLink.First.IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Confirmation_FullyPaid_HidesPayOnlineButton()
    {
        var bookingId = SeedBooking(paidInFull: true);

        var page = await _fx.Browser.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/PublicRegistration/Confirmation?bookingId={bookingId}");
        response!.Status.Should().Be(200);

        var payLink = page.Locator("a[href*='/OnlinePayment/Checkout']");
        (await payLink.CountAsync()).Should().Be(0, "a fully-paid booking must not offer online payment");
    }

    [Fact]
    public async Task Checkout_FullyPaidBooking_RedirectsToConfirmation_WithoutCharging()
    {
        var bookingId = SeedBooking(paidInFull: true);

        // The amount-due guard short-circuits before any provider call, so this works without a
        // Stripe key: amountDue <= 0 -> redirect to the confirmation page (never the hosted checkout).
        var page = await _fx.Browser.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/OnlinePayment/Checkout?bookingId={bookingId}");

        response!.Status.Should().Be(200);
        page.Url.Should().Contain($"/PublicRegistration/Confirmation", "a paid booking is sent back to confirmation, not to checkout");
        page.Url.Should().NotContain("stripe.com");
    }
}
