using System.Globalization;
using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Services.Email;

namespace Cedeva.Tests.Services.Email;

public class EmailVariableReplacementServiceTests
{
    private readonly EmailVariableReplacementService _sut = new();

    public EmailVariableReplacementServiceTests()
    {
        // The service formats amounts/dates with CurrentCulture (ToString("F2") / "dd/MM/yyyy").
        // Pin Invariant so the expected "125.00" style assertions are deterministic across machines.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private static (Booking booking, Organisation org) BuildFullBooking()
    {
        var parent = new Parent
        {
            FirstName = "Marie",
            LastName = "Dupont",
            Email = "marie@example.be",
            MobilePhoneNumber = "0470123456"
        };
        var child = new Child
        {
            FirstName = "Léa",
            LastName = "Dupont",
            BirthDate = new DateTime(2015, 3, 10),
            Parent = parent
        };
        var activity = new Activity
        {
            Name = "Stage Été",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 5),
            PricePerDay = 25m
        };
        var booking = new Booking
        {
            Id = 42,
            Child = child,
            Activity = activity,
            Group = new ActivityGroup { Label = "Les Lions" },
            TotalAmount = 125m,
            PaidAmount = 100m,
            StructuredCommunication = "+++000/0000/00042+++"
        };
        var org = new Organisation
        {
            Name = "Centre ABC",
            BankAccountNumber = "BE68539007547034",
            BankAccountName = "Centre ABC ASBL"
        };
        return (booking, org);
    }

    [Fact]
    public void ReplaceVariables_ResolvesAllChildAndParentVariables()
    {
        var (booking, org) = BuildFullBooking();
        const string template =
            "%prenom_enfant%|%nom_enfant%|%nom_complet_enfant%|%date_naissance_enfant%|" +
            "%prenom_parent%|%nom_parent%|%nom_complet_parent%|%email_parent%|%telephone_parent%";

        var result = _sut.ReplaceVariables(template, booking, org);

        result.Should().Be(
            "Léa|Dupont|Léa Dupont|10/03/2015|" +
            "Marie|Dupont|Marie Dupont|marie@example.be|0470123456");
    }

    [Fact]
    public void ReplaceVariables_ResolvesBookingActivityAndOrganisationVariables()
    {
        var (booking, org) = BuildFullBooking();
        const string template =
            "%montant_total%|%montant_paye%|%montant_restant%|%communication_structuree%|%numero_reservation%|%groupe%|" +
            "%nom_activite%|%date_debut_activite%|%date_fin_activite%|%prix_par_jour%|" +
            "%nom_organisation%|%numero_compte%|%titulaire_compte%";

        var result = _sut.ReplaceVariables(template, booking, org);

        result.Should().Be(
            "125.00|100.00|25.00|+++000/0000/00042+++|42|Les Lions|" +
            "Stage Été|01/07/2026|05/07/2026|25.00|" +
            "Centre ABC|BE68539007547034|Centre ABC ASBL");
    }

    [Fact]
    public void ReplaceVariables_IsCaseInsensitive()
    {
        var (booking, org) = BuildFullBooking();

        _sut.ReplaceVariables("%PRENOM_ENFANT%", booking, org).Should().Be("Léa");
    }

    [Fact]
    public void ReplaceVariables_KeepsUnknownVariableUntouched()
    {
        var (booking, org) = BuildFullBooking();

        _sut.ReplaceVariables("Hello %inconnu%", booking, org).Should().Be("Hello %inconnu%");
    }

    [Fact]
    public void ReplaceVariables_WithNullNavigationChains_ReplacesWithEmptyString()
    {
        // Booking without Child / Activity / Group
        var booking = new Booking { Id = 7, TotalAmount = 0m, PaidAmount = 0m };
        var org = new Organisation { Name = "Org" };

        _sut.ReplaceVariables("[%prenom_enfant%][%email_parent%][%nom_activite%][%groupe%]", booking, org)
            .Should().Be("[][][][]");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ReplaceVariables_WithNullOrEmptyTemplate_ReturnsInput(string? template)
    {
        var (booking, org) = BuildFullBooking();

        _sut.ReplaceVariables(template!, booking, org).Should().Be(template);
    }

    [Fact]
    public void GetAvailableVariables_Returns22Variables()
    {
        _sut.GetAvailableVariables().Should().HaveCount(22);
    }
}
