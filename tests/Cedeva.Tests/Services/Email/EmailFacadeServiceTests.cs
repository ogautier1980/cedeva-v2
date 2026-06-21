using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Services;
using Cedeva.Infrastructure.Services.Email;
using Cedeva.Tests.TestSupport;
using NSubstitute;

namespace Cedeva.Tests.Services.Email;

/// <summary>
/// Covers <see cref="EmailFacadeService.SendBookingTemplateAsync"/> — the shared helper that powers
/// the booking-confirmation and new-registration-notification emails: it renders the organisation's
/// default template for a type (resolving %variables%) and sends it, or returns false so the caller
/// can fall back. The Brevo sender is replaced by an in-memory <see cref="FakeEmailService"/>.
/// </summary>
public class EmailFacadeServiceTests
{
    private static (Organisation org, Booking booking) SeedGraph(SqliteTestContext db)
    {
        var org = TestData.Organisation();
        var activity = TestData.Activity(org, "Stage Multisports");
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);
        var booking = TestData.Booking(child, activity, group: null, totalAmount: 120m, paidAmount: 0m);
        db.Context.AddRange(org, activity, parent, child, booking);
        db.Context.SaveChanges();
        return (org, booking);
    }

    private static void SeedTemplate(SqliteTestContext db, int orgId, EmailTemplateType type, string subject, string html)
    {
        db.Context.EmailTemplates.Add(new EmailTemplate
        {
            OrganisationId = orgId,
            Name = type.ToString(),
            TemplateType = type,
            Subject = subject,
            HtmlContent = html,
            IsDefault = true
        });
        db.Context.SaveChanges();
    }

    [Fact]
    public async Task SendBookingTemplateAsync_RendersTemplateVariables_AndSends()
    {
        using var db = new SqliteTestContext();
        var (org, booking) = SeedGraph(db);
        SeedTemplate(db, org.Id, EmailTemplateType.BookingConfirmation,
            "Inscription %nom_complet_enfant%", "<p>%nom_complet_enfant% — %nom_activite% — %montant_total%</p>");

        var fake = new FakeEmailService();
        var facade = new EmailFacadeService(fake, Substitute.For<IEmailRecipientService>(),
            new EmailVariableReplacementService(),
            new EmailTemplateService(db.NewContext(FakeCurrentUserService.Admin())));

        var sent = await facade.SendBookingTemplateAsync(
            EmailTemplateType.BookingConfirmation, org.Id, new[] { "parent@test.be" }, booking, org);

        sent.Should().BeTrue();
        fake.Sent.Should().ContainSingle();
        fake.Sent[0].To.Should().Contain("parent@test.be");
        fake.Sent[0].Subject.Should().Contain("Chloé Enfant");
        fake.Sent[0].Html.Should().Contain("Chloé Enfant").And.Contain("Stage Multisports").And.Contain("120");
    }

    [Fact]
    public async Task SendBookingTemplateAsync_NoTemplate_ReturnsFalse_AndSendsNothing()
    {
        using var db = new SqliteTestContext();
        var (org, booking) = SeedGraph(db);
        // No template of this type seeded.

        var fake = new FakeEmailService();
        var facade = new EmailFacadeService(fake, Substitute.For<IEmailRecipientService>(),
            new EmailVariableReplacementService(),
            new EmailTemplateService(db.NewContext(FakeCurrentUserService.Admin())));

        var sent = await facade.SendBookingTemplateAsync(
            EmailTemplateType.NewRegistrationNotification, org.Id, new[] { "org@test.be" }, booking, org);

        sent.Should().BeFalse("no default template exists, so the caller should fall back");
        fake.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendBookingTemplateAsync_NoRecipients_ReturnsFalse()
    {
        using var db = new SqliteTestContext();
        var (org, booking) = SeedGraph(db);
        SeedTemplate(db, org.Id, EmailTemplateType.BookingConfirmation, "x", "<p>y</p>");

        var fake = new FakeEmailService();
        var facade = new EmailFacadeService(fake, Substitute.For<IEmailRecipientService>(),
            new EmailVariableReplacementService(),
            new EmailTemplateService(db.NewContext(FakeCurrentUserService.Admin())));

        var sent = await facade.SendBookingTemplateAsync(
            EmailTemplateType.BookingConfirmation, org.Id, new[] { "", "   " }, booking, org);

        sent.Should().BeFalse();
        fake.Sent.Should().BeEmpty();
    }
}
