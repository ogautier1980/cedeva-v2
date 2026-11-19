using System.Net;
using Autofac;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Fiscal attestation (form 281.86) from the client's model of 19/11/2026: one document per parent,
/// a Cadre II per child, amounts = payments of the calendar year, days = days present before the
/// child's 14th birthday; e-mailed one at a time (parent page) or all at once (FiscalAttestations).
/// The 2025 ceiling (16.90 €) is seeded; 2026 is deliberately left unconfigured.
/// </summary>
[Collection("WebApp")]
public class FiscalAttestationTests
{
    private sealed record Seeded(int OrgId, int ParentId);

    /// <summary>
    /// Chloé: 5-day stage in July 2025, present 4 days, 100 € paid in 2025 (+ 80 € paid in January
    /// 2026). Léo: same stage, present 5 days, 60 € paid in 2025. A cancelled payment is ignored.
    /// </summary>
    private static Seeded SeedFamily(CedevaWebApplicationFactory factory, DateTime? leoBirthDate = null)
    {
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation("Plaine de Bossière");
            org.ResponsibleName = "Jean Dupont";
            parent = TestData.Parent(org);
            var chloe = TestData.Child(parent);
            var leo = TestData.Child(parent);
            leo.FirstName = "Léo";
            leo.NationalRegisterNumber = "18030112399";
            if (leoBirthDate.HasValue) leo.BirthDate = leoBirthDate.Value;

            var activity = TestData.Activity(org, "Plaine Été 2025");
            activity.StartDate = new DateTime(2025, 7, 7);
            activity.EndDate = new DateTime(2025, 7, 11);
            var days = Enumerable.Range(0, 5)
                .Select(i => new ActivityDay { Label = $"J{i + 1}", DayDate = activity.StartDate.AddDays(i), IsActive = true, Week = 1 })
                .ToList();
            foreach (var day in days) activity.Days.Add(day);

            var chloeBooking = TestData.Booking(chloe, activity, null, totalAmount: 180m, paidAmount: 180m);
            chloeBooking.Days = days.Select((d, i) => new BookingDay { ActivityDay = d, IsReserved = true, IsPresent = i != 2 }).ToList();
            var leoBooking = TestData.Booking(leo, activity, null, totalAmount: 60m, paidAmount: 60m);
            leoBooking.Days = days.Select(d => new BookingDay { ActivityDay = d, IsReserved = true, IsPresent = true }).ToList();

            ctx.AddRange(org, parent, chloe, leo, activity, chloeBooking, leoBooking,
                Paid(chloeBooking, 100m, new DateTime(2025, 6, 15)),
                Paid(chloeBooking, 80m, new DateTime(2026, 1, 10)),
                Paid(leoBooking, 60m, new DateTime(2025, 7, 1)),
                new Payment { Booking = leoBooking, Amount = 999m, PaymentDate = new DateTime(2025, 7, 2), Status = PaymentStatus.Cancelled, PaymentMethod = PaymentMethod.Online });
            return 0;
        });
        return new Seeded(org.Id, parent.Id);
    }

    private static Payment Paid(Booking booking, decimal amount, DateTime date) =>
        new() { Booking = booking, Amount = amount, PaymentDate = date, Status = PaymentStatus.Paid, PaymentMethod = PaymentMethod.BankTransfer };

    private static async Task<string> GetText(HttpClient client, string url) =>
        WebUtility.HtmlDecode(await client.GetStringAsync(url));

    // ---- Parent page --------------------------------------------------------------------

    [Fact]
    public async Task Page_2025_ListsEachChildWithTheDaysPresentAndThePaymentsOfTheYear()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFamily(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var html = await GetText(client, $"/Parents/FiscalAttestation/{s.ParentId}?year=2025");

        html.Should().Contain("Chloé").And.Contain("Léo").And.Contain("Plaine Été 2025");
        html.Should().Contain("07/07/2025 – 11/07/2025");
        html.Should().Contain("100,00").And.Contain("60,00").And.Contain("160,00");
        html.Should().NotContain("80,00", "January 2026 payments belong to the 2026 attestation");
        html.Should().NotContain("999", "only paid payments count");
        html.Should().Contain("Télécharger le PDF");
    }

    [Fact]
    public async Task Page_DefaultsToTheMostRecentYear_AndWarnsWhenItsCeilingIsNotSet()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFamily(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var html = await GetText(client, $"/Parents/FiscalAttestation/{s.ParentId}");

        html.Should().Contain("Attestation fiscale 2026");
        html.Should().Contain("Le plafond par jour de 2026 n'est pas encore renseigné");
        html.Should().NotContain("Télécharger le PDF");
    }

    [Fact]
    public async Task Page_DaysAfterThe14thBirthday_AreLeftOutAndTheAmountProrated()
    {
        using var factory = new CedevaWebApplicationFactory();
        // Léo turns 14 on 09/07/2025: only 07 and 08/07 count, 2 of his 5 present days → 24 € of 60 €.
        var s = SeedFamily(factory, leoBirthDate: new DateTime(2011, 7, 9));
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var html = await GetText(client, $"/Parents/FiscalAttestation/{s.ParentId}?year=2025");

        html.Should().Contain("07/07/2025 – 08/07/2025");
        html.Should().Contain("24,00");
    }

    [Fact]
    public async Task Page_UnknownParent_OrOtherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFamily(factory);

        (await factory.CreateClientFor("u1", s.OrgId, "Coordinator").GetAsync("/Parents/FiscalAttestation/999999"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await factory.CreateClientFor("u2", 999999, "Coordinator").GetAsync($"/Parents/FiscalAttestation/{s.ParentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Page_ShowsTheOrderNumberOfEachChildsAttestation()
    {
        // SPF guide § 3.1: every attestation carries an order number (year-parent-child).
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFamily(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var html = await GetText(client, $"/Parents/FiscalAttestation/{s.ParentId}?year=2025");

        html.Should().MatchRegex($@"N° d'ordre 2025-{s.ParentId}-\d+");
    }

    [Fact]
    public async Task Page_WarnsWhenAChildHasNoNationalRegisterNumber()
    {
        // SPF guide § 3.2-3.3: the national register numbers of the debtor and of the child are required.
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFamily(factory);
        using (var db = factory.NewDbContext())
        {
            db.Children.IgnoreQueryFilters().First(c => c.FirstName == "Léo").NationalRegisterNumber = "";
            db.SaveChanges();
        }
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var html = await GetText(client, $"/Parents/FiscalAttestation/{s.ParentId}?year=2025");

        html.Should().Contain("Le numéro de registre national du parent ou d'un enfant manque");
    }

    [Fact]
    public async Task OrganisationSettings_SaveTheResponsiblesTitle()
    {
        // SPF guide § 4: the signatory's name and « qualité ».
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFamily(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync($"/Organisations/Edit/{s.OrgId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = s.OrgId.ToString(),
            ["Name"] = "Plaine de Bossière",
            ["Description"] = "Organisation de stages pour enfants",
            ["Street"] = "Rue de Test",
            ["City"] = "Bruxelles",
            ["PostalCode"] = "1000",
            ["Country"] = "Belgium",
            ["ResponsibleName"] = "Jean Dupont",
            ["ResponsibleTitle"] = "Président"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var db = factory.NewDbContext();
        db.Organisations.Single(o => o.Id == s.OrgId).ResponsibleTitle.Should().Be("Président");
    }

    // ---- PDF and e-mail -----------------------------------------------------------------

    [Fact]
    public async Task Pdf_2025_IsAPdfNamedAfterTheParent()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFamily(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Parents/FiscalAttestationPdf/{s.ParentId}?year=2025");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("Attestation-fiscale-2025-Parent.pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Pdf_YearWithoutCeiling_RedirectsToThePage()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFamily(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Parents/FiscalAttestationPdf/{s.ParentId}?year=2026");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain($"/Parents/FiscalAttestation/{s.ParentId}");
    }

    [Fact]
    public async Task Send_EmailsThePdfToTheParent()
    {
        var fake = new FakeEmailService();
        using var factory = new CedevaWebApplicationFactory
        {
            ConfigureExtraTestContainer = b => b.RegisterInstance(fake).As<IEmailService>()
        };
        var s = SeedFamily(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync($"/Parents/SendFiscalAttestation/{s.ParentId}?year=2025",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        var message = fake.Sent.Should().ContainSingle().Subject;
        message.To.Should().Equal("paul.parent@test.be");
        message.Subject.Should().Contain("2025").And.Contain("Plaine de Bossière");
        Path.GetFileName(message.AttachmentPath).Should().Be("Attestation-fiscale-2025-Parent.pdf");
        System.Text.Encoding.ASCII.GetString(message.AttachmentBytes!, 0, 4).Should().Be("%PDF");
        File.Exists(message.AttachmentPath).Should().BeFalse("the temporary PDF is deleted after sending");
    }

    // ---- Organisation-wide sending --------------------------------------------------------

    [Fact]
    public async Task SendAll_EmailsEveryParentOfTheYear_AndCountsThoseWithoutEmail()
    {
        var fake = new FakeEmailService();
        using var factory = new CedevaWebApplicationFactory
        {
            ConfigureExtraTestContainer = b => b.RegisterInstance(fake).As<IEmailService>()
        };
        var s = SeedFamily(factory);
        factory.Seed(ctx =>
        {
            // A second family of the same organisation, without e-mail address.
            var org = ctx.Organisations.IgnoreQueryFilters().Single(o => o.Id == s.OrgId);
            var activity = ctx.Activities.IgnoreQueryFilters().Include(a => a.Days).Single(a => a.OrganisationId == s.OrgId);
            var parent = TestData.Parent(org);
            parent.LastName = "SansMail";
            parent.Email = "";
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 20m, 20m);
            booking.Days = activity.Days.Take(1).Select(d => new BookingDay { ActivityDayId = d.DayId, IsReserved = true, IsPresent = true }).ToList();
            ctx.AddRange(parent, child, booking, Paid(booking, 20m, new DateTime(2025, 7, 7)));
            return 0;
        });
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var page = await GetText(client, "/FiscalAttestations?year=2025");
        page.Should().Contain("2 parent(s)").And.Contain("Pas d'adresse e-mail");

        var response = await client.PostAsync("/FiscalAttestations/SendAll?year=2025",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        fake.Sent.Should().ContainSingle().Which.To.Should().Equal("paul.parent@test.be");
    }

    [Fact]
    public async Task SendAll_YearWithoutCeiling_SendsNothing()
    {
        var fake = new FakeEmailService();
        using var factory = new CedevaWebApplicationFactory
        {
            ConfigureExtraTestContainer = b => b.RegisterInstance(fake).As<IEmailService>()
        };
        var s = SeedFamily(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        await client.PostAsync("/FiscalAttestations/SendAll?year=2026",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        fake.Sent.Should().BeEmpty();
    }

    // ---- Admin: yearly ceilings -----------------------------------------------------------

    [Fact]
    public async Task FiscalYears_AdminAddsAYear_DuplicateRefused_CoordinatorForbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFamily(factory);
        var admin = factory.CreateClientFor("admin", null, "Admin");
        FormUrlEncodedContent Form(int year) => new(new Dictionary<string, string>
        {
            ["Year"] = year.ToString(), ["MaxDailyAmount"] = "17,50", ["TaxCode"] = "1384"
        });

        (await admin.PostAsync("/FiscalYears/Create", Form(2026))).StatusCode.Should().Be(HttpStatusCode.Found);
        (await admin.PostAsync("/FiscalYears/Create", Form(2025))).StatusCode.Should().Be(HttpStatusCode.OK, "2025 already exists");

        await using (var db = factory.NewDbContext())
        {
            (await db.FiscalYearSettings.SingleAsync(y => y.Year == 2026)).MaxDailyAmount.Should().Be(17.50m);
            (await db.FiscalYearSettings.CountAsync()).Should().Be(2);
        }

        var coordinator = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
        (await coordinator.GetAsync("/FiscalYears")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FiscalYears_DeleteButtonPostsHere_AndDeletes()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var admin = factory.CreateClientFor("admin", null, "Admin");

        (await admin.GetStringAsync("/FiscalYears")).Should().Contain("action=\"/FiscalYears/Delete/1\"");
        var response = await admin.PostAsync("/FiscalYears/Delete/1",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        await using var db = factory.NewDbContext();
        (await db.FiscalYearSettings.AnyAsync()).Should().BeFalse();
    }
}
