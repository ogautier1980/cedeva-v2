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

    /// <summary>Signature image of the responsible person, printed in section 4 when set.</summary>
    public string? ResponsibleSignatureUrl { get; set; }
    public string OrganisationAddress { get; set; } = string.Empty;

    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public DateTime ChildBirthDate { get; set; }

    public string ActivityName { get; set; } = string.Empty;

    /// <summary>The week(s) booked ("Semaine 2") when the activity spans several weeks, else null.</summary>
    public string? WeeksLabel { get; set; }

    /// <summary>Period the child is booked for (first to last reserved day) — the activity's own
    /// dates only when no day is reserved.</summary>
    public DateTime ActivityStartDate { get; set; }
    public DateTime ActivityEndDate { get; set; }

    /// <summary>Number of days the child was actually marked present (BookingDay.IsPresent),
    /// not merely reserved — this is what the mutuality reimburses against.</summary>
    public int DaysAttended { get; set; }

    public decimal AmountPaid { get; set; }
    public DateTime IssuedDate { get; set; } = DateTime.Today;
}
