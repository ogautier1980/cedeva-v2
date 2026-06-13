using System.Net;
using System.Text;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration tests for the anonymous <c>OnlinePaymentController</c>.
///
/// Stripe is NOT configured in the test app (Stripe:SecretKey is empty in appsettings.json),
/// so a real hosted checkout cannot be created: <c>Checkout</c> only succeeds (without touching
/// Stripe) for a booking that has nothing left to pay. The webhook gateway returns null for any
/// unsigned/invalid payload, so the webhook endpoint always answers gracefully (400, never 500).
/// </summary>
[Collection("WebApp")]
public class OnlinePaymentControllerIntegrationTests
{
    // ----- Endpoints are anonymous: no auth header required (controller is [AllowAnonymous]). -----

    [Fact]
    public async Task Checkout_UnknownBooking_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0); // create schema only

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/OnlinePayment/Checkout?bookingId=999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Checkout_NothingDue_RedirectsToConfirmation_WithoutCallingStripe()
    {
        using var factory = new CedevaWebApplicationFactory();
        // Fully-paid booking: amountDue == 0, so the controller redirects before touching Stripe.
        var booking = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var activity = TestData.Activity(org);
            var b = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 100m);
            ctx.AddRange(org, parent, child, activity, b);
            return b;
        });

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync($"/OnlinePayment/Checkout?bookingId={booking.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Found); // 302
        var location = response.Headers.Location!.ToString();
        location.Should().Contain("Confirmation");
        location.Should().Contain($"bookingId={booking.Id}");
    }

    [Fact]
    public async Task Checkout_OverpaidBooking_TreatedAsNothingDue_RedirectsToConfirmation()
    {
        using var factory = new CedevaWebApplicationFactory();
        // amountDue is negative (paid more than total) => still <= 0 => redirect, no Stripe call.
        var booking = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var activity = TestData.Activity(org);
            var b = TestData.Booking(child, activity, group: null, totalAmount: 50m, paidAmount: 75m);
            ctx.AddRange(org, parent, child, activity, b);
            return b;
        });

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync($"/OnlinePayment/Checkout?bookingId={booking.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Confirmation");
    }

    [Fact]
    public async Task Checkout_AmountDue_StripeNotConfigured_DoesNotRedirectToProvider()
    {
        using var factory = new CedevaWebApplicationFactory();
        // amountDue > 0 forces a real CreateCheckoutAsync call. The outcome depends on whether a
        // Stripe SecretKey is configured for the host (appsettings is empty, but a developer's
        // user-secrets or CI may supply one):
        //   * configured   -> 302 redirect to the provider's hosted checkout (NOT our Confirmation),
        //   * not configured -> the gateway throws InvalidOperationException, surfaced as a 500
        //                       (Development exception page) or rethrown by the TestServer.
        // The invariant in every case: it must NOT take the "nothing due" shortcut that redirects
        // back to our own PublicRegistration/Confirmation page.
        var booking = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var activity = TestData.Activity(org);
            var b = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 0m);
            ctx.AddRange(org, parent, child, activity, b);
            return b;
        });

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        HttpResponseMessage? response;
        try
        {
            response = await client.GetAsync($"/OnlinePayment/Checkout?bookingId={booking.Id}");
        }
        catch (InvalidOperationException)
        {
            // Stripe not configured and the TestServer rethrew the unhandled gateway exception:
            // definitively not the "nothing due" Confirmation redirect.
            return;
        }

        if (response.StatusCode == HttpStatusCode.Found)
        {
            // Stripe configured: the redirect must go to the provider, never our Confirmation page.
            var location = response.Headers.Location!.ToString();
            location.Should().NotContain("Confirmation");
        }
        else
        {
            // Stripe not configured: the unhandled gateway exception surfaces as a server error.
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
    }

    // ----- Return: pure redirect, regardless of whether the booking exists. -----

    [Fact]
    public async Task Return_RedirectsToConfirmation()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/OnlinePayment/Return?bookingId=123");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        var location = response.Headers.Location!.ToString();
        location.Should().Contain("Confirmation");
        location.Should().Contain("bookingId=123");
    }

    // ----- Webhook: must reject unsigned/invalid payloads gracefully (400), never 500. -----

    [Fact]
    public async Task Webhook_WithoutSignatureHeader_ReturnsBadRequest()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var content = new StringContent("{\"id\":\"evt_1\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/OnlinePayment/Webhook", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_ReturnsBadRequest_NotServerError()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var content = new StringContent("{\"id\":\"evt_1\"}", Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "t=1,v1=deadbeef"); // bogus signature

        var response = await client.PostAsync("/OnlinePayment/Webhook", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_WithEmptyBodyAndNoSignature_ReturnsBadRequest()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/OnlinePayment/Webhook", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
