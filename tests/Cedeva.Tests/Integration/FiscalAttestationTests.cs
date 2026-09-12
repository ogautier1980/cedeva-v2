using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage for ParentsController.FiscalAttestation (Lot H) — grouped by association (all of the
/// parent's children, all activities of the organisation) for a given fiscal year, as opposed to
/// Bookings/MutualityAttestation (Lot K #3) which is per child/activity.
/// </summary>
[Collection("WebApp")]
public class FiscalAttestationTests
{
    private static (int OrgId, int ParentId) SeedParentWithBookingsAcrossTwoYears(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        Parent parent = null!;

        factory.Seed(ctx =>
        {
            org = TestData.Organisation("Plaine de Bossière");
            org.ResponsibleName = "Jean Dupont";
            org.CompanyNumber = "0123.456.789";

            parent = TestData.Parent(org);
            var child1 = TestData.Child(parent);
            child1.FirstName = "Chloé";
            var child2 = TestData.Child(parent);
            child2.FirstName = "Léo";
            child2.NationalRegisterNumber = "18030112399";

            var activity2026 = TestData.Activity(org, "Stage Été 2026");
            activity2026.StartDate = new DateTime(2026, 7, 6);
            activity2026.EndDate = new DateTime(2026, 7, 10);

            var activity2027 = TestData.Activity(org, "Stage Été 2027");
            activity2027.StartDate = new DateTime(2027, 7, 5);
            activity2027.EndDate = new DateTime(2027, 7, 9);

            var confirmedPaid2026 = TestData.Booking(child1, activity2026, group: null, totalAmount: 100m, paidAmount: 100m);
            var confirmedPaid2026Child2 = TestData.Booking(child2, activity2026, group: null, totalAmount: 60m, paidAmount: 60m);
            var confirmedPaid2027 = TestData.Booking(child1, activity2027, group: null, totalAmount: 80m, paidAmount: 80m);
            var unconfirmed2026 = TestData.Booking(child1, activity2026, group: null, totalAmount: 50m, paidAmount: 50m);
            unconfirmed2026.IsConfirmed = false;
            var unpaid2026 = TestData.Booking(child1, activity2026, group: null, totalAmount: 30m, paidAmount: 0m);

            ctx.AddRange(org, parent, child1, child2, activity2026, activity2027,
                confirmedPaid2026, confirmedPaid2026Child2, confirmedPaid2027, unconfirmed2026, unpaid2026);
            return 0;
        });

        return (org.Id, parent.Id);
    }

    [Fact]
    public async Task FiscalAttestation_RequestedYear_ListsOnlyThatYearsConfirmedPaidBookings()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, parentId) = SeedParentWithBookingsAcrossTwoYears(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/Parents/FiscalAttestation/{parentId}?year=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        html.Should().Contain("Plaine de Bossière");
        html.Should().Contain("0123.456.789");
        html.Should().Contain("Jean Dupont");
        html.Should().Contain("Stage Été 2026");
        html.Should().Contain("Chloé");
        html.Should().Contain("Léo");
        // 2027 booking, unconfirmed booking and unpaid booking must not appear.
        html.Should().NotContain("Stage Été 2027");
        // Total = 100 (child1) + 60 (child2) = 160,00 €; the excluded unconfirmed/unpaid amounts (50/30) must not be summed in.
        html.Should().Contain("160,00");
    }

    [Fact]
    public async Task FiscalAttestation_NoYearGiven_DefaultsToMostRecentAvailableYear()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, parentId) = SeedParentWithBookingsAcrossTwoYears(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/Parents/FiscalAttestation/{parentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        // Most recent year with bookings is 2027 (only child1, 80 €).
        html.Should().Contain("Stage Été 2027");
        html.Should().Contain("80,00");
    }

    [Fact]
    public async Task FiscalAttestation_UnknownParent_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync("/Parents/FiscalAttestation/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FiscalAttestation_CoordinatorOfOtherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (_, parentId) = SeedParentWithBookingsAcrossTwoYears(factory);

        var client = factory.CreateClientFor("u2", organisationId: 999999, "Coordinator");
        var response = await client.GetAsync($"/Parents/FiscalAttestation/{parentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FiscalAttestation_NoBookingsAtAll_RendersEmptyWithoutError()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.GetAsync($"/Parents/FiscalAttestation/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
