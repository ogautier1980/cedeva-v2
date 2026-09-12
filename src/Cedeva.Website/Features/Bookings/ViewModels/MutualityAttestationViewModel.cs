namespace Cedeva.Website.Features.Bookings.ViewModels;

/// <summary>
/// Lot K #3 — attestation de fréquentation pour mutuelle, généralisée par organisation (logo, nom
/// du responsable, adresse — chacun personnalisable via Organisations/Edit). Une par booking.
/// </summary>
public class MutualityAttestationViewModel
{
    public string OrganisationName { get; set; } = string.Empty;
    public string? OrganisationLogoUrl { get; set; }
    public string? ResponsibleName { get; set; }
    public string OrganisationAddress { get; set; } = string.Empty;

    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public DateTime ChildBirthDate { get; set; }

    public string ActivityName { get; set; } = string.Empty;
    public DateTime ActivityStartDate { get; set; }
    public DateTime ActivityEndDate { get; set; }

    /// <summary>Number of days the child was actually marked present (BookingDay.IsPresent),
    /// not merely reserved — this is what the mutuality reimburses against.</summary>
    public int DaysAttended { get; set; }

    public decimal AmountPaid { get; set; }
    public DateTime IssuedDate { get; set; } = DateTime.Today;
}
