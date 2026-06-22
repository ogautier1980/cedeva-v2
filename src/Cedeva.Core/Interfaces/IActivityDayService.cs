using Cedeva.Core.Entities;

namespace Cedeva.Core.Interfaces;

/// <summary>Outcome of applying day activation/deactivation changes from the Edit form.</summary>
public enum DayActivationOutcome
{
    Applied,
    NeedsRemoveConfirmation,
    NeedsActivateInfo
}

/// <summary>
/// Result of a day activation/deactivation pass. When confirmation/info is needed, carries the data
/// the controller needs to build the TempData message and re-render the view (no mutation persisted).
/// </summary>
public record DayActivationResult(
    DayActivationOutcome Outcome,
    int AffectedBookings = 0,
    IReadOnlyList<string>? DayLabels = null,
    IReadOnlyList<int>? RemainingActiveDayIds = null,
    IReadOnlyList<int>? ActivatedDayIds = null);

/// <summary>Outcome of the AJAX day-range editor (extend/shrink one edge).</summary>
public enum AdjustDaysOutcome
{
    NotFound,
    BadRequest,
    CannotRemoveLastDay,
    NeedsConfirmation,
    Success
}

/// <summary>One day in the AJAX adjust response (serialised to JSON by the controller).</summary>
public record AdjustDayDto(int DayId, string Label, string Date, bool IsActive, int Week);

/// <summary>Result of <see cref="IActivityDayService.AdjustAsync"/>.</summary>
public record AdjustDaysResult(
    AdjustDaysOutcome Outcome,
    int ReservedCount = 0,
    string? Label = null,
    string? StartDate = null,
    string? EndDate = null,
    int ActiveDaysCount = 0,
    IReadOnlyList<AdjustDayDto>? Days = null);

/// <summary>
/// Booking-aware activity day operations: apply Edit-form activation/deactivation (removing/adding
/// BookingDays, with confirmation when reserved days are affected) and the AJAX extend/shrink editor
/// (which decrements booking totals by one PricePerDay per removed reserved day). View/JSON concerns
/// stay in the controller — these methods return result objects.
/// </summary>
public interface IActivityDayService
{
    /// <summary>
    /// Applies the posted active-day set to a tracked activity. Does not call SaveChanges (the Edit
    /// action saves) — but returns a non-Applied outcome (no mutation) when confirmation/info is due.
    /// </summary>
    Task<DayActivationResult> ApplyDayActivationChangesAsync(
        Activity activity, IReadOnlyList<int> activeDayIds, bool addDaysToBookings, bool removeDaysConfirmed,
        CancellationToken ct = default);

    /// <summary>Extends/shrinks the activity's date range by one edge day; persists its own changes.</summary>
    Task<AdjustDaysResult> AdjustAsync(int activityId, string edge, string op, bool confirmed, CancellationToken ct = default);
}
