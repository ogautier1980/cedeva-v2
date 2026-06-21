using System.Net;
using Autofac;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Exercises the email-sending pipeline of <c>ActivityManagementController.SendEmail</c> (POST) end
/// to end through the real app, with the Autofac-registered Brevo sender swapped for an in-memory
/// <see cref="FakeEmailService"/>. Covers the two send modes (one mail per child, one per parent),
/// the recipient filtering / logging, and the "no recipients" short-circuit — paths that the
/// GET-only tests never reach.
/// </summary>
[Collection("WebApp")]
public class ActivityManagementSendEmailTests
{
    private static CedevaWebApplicationFactory NewFactory(FakeEmailService fake) => new()
    {
        ConfigureExtraTestContainer = b => b.RegisterInstance(fake).As<IEmailService>(),
    };

    private static (Organisation org, Activity activity) SeedActivityWithConfirmedBooking(
        CedevaWebApplicationFactory factory, bool withBooking)
    {
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            if (withBooking)
            {
                var parent = TestData.Parent(org);
                var child = TestData.Child(parent);
                var booking = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 0m); // IsConfirmed
                ctx.AddRange(parent, child, booking);
            }
            return 0;
        });
        return (org, activity);
    }

    private static FormUrlEncodedContent Form(int activityId, bool perChild) =>
        new(new Dictionary<string, string>
        {
            ["ActivityId"] = activityId.ToString(),
            ["SelectedRecipient"] = "allparents",
            ["Subject"] = "Bonjour {prenom_enfant}",
            ["Message"] = "Message de test",
            ["SendSeparateEmailPerChild"] = perChild ? "true" : "false",
        });

    [Fact]
    public async Task SendEmail_PerChild_SendsToParent_LogsAndRedirects()
    {
        var fake = new FakeEmailService();
        using var factory = NewFactory(fake);
        var (org, activity) = SeedActivityWithConfirmedBooking(factory, withBooking: true);

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync("/ActivityManagement/SendEmail", Form(activity.Id, perChild: true));

        response.StatusCode.Should().Be(HttpStatusCode.Found, "a successful send redirects back to the form");
        fake.Sent.Should().ContainSingle("there is one confirmed booking / parent");
        fake.Sent[0].To.Should().Contain("paul.parent@test.be");

        using var db = factory.NewDbContext();
        (await db.EmailsSent.AnyAsync(e => e.ActivityId == activity.Id))
            .Should().BeTrue("the send must be logged");
    }

    [Fact]
    public async Task SendEmail_PerParent_SendsToUniqueParentEmails_AndRedirects()
    {
        var fake = new FakeEmailService();
        using var factory = NewFactory(fake);
        var (org, activity) = SeedActivityWithConfirmedBooking(factory, withBooking: true);

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync("/ActivityManagement/SendEmail", Form(activity.Id, perChild: false));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        fake.Sent.Should().ContainSingle();
        fake.Sent[0].To.Should().Contain("paul.parent@test.be");
    }

    [Fact]
    public async Task SendEmail_NoConfirmedBookings_ReturnsViewWithError_AndSendsNothing()
    {
        var fake = new FakeEmailService();
        using var factory = NewFactory(fake);
        var (org, activity) = SeedActivityWithConfirmedBooking(factory, withBooking: false);

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync("/ActivityManagement/SendEmail", Form(activity.Id, perChild: true));

        // No recipients -> the controller re-renders the form (200) instead of redirecting.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.Sent.Should().BeEmpty("there is nobody to email");

        using var db = factory.NewDbContext();
        (await db.EmailsSent.AnyAsync(e => e.ActivityId == activity.Id))
            .Should().BeFalse("nothing was sent, so nothing is logged");
    }
}
