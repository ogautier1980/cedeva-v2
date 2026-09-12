using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// Lot K #4 — caps how many children born in a given year may register for an activity (e.g.
/// "max 24 children born in 2020"). Applied per activity, not per week: in practice a booking
/// reserves all of the activity's active days at once (no partial-week registration exists), so a
/// per-activity cap already behaves as "per week" for the common case (one activity = one week of
/// camp) — see docs/notion/BACKLOG.md Lot K for the full reasoning.
/// </summary>
public class ActivityBirthYearQuota : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    [Range(1900, 2100, ErrorMessage = "Validation.AmountRange")]
    public int BirthYear { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Range(1, 9999, ErrorMessage = "Validation.AmountRange")]
    public int MaxChildren { get; set; }
}
