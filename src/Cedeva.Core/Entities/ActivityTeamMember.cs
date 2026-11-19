using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

/// <summary>
/// A team member assigned to an activity, with what depends on that activity (Thomas, 19/11/2026:
/// the member sheet — the « réserve » — keeps the identity only, nothing about pay): status
/// (volunteer / compensated) and daily rate. The rate applies whatever the status: a volunteer's
/// expense allowance also changes from one activity to the next.
/// </summary>
public class ActivityTeamMember
{
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public int TeamMemberId { get; set; }
    public TeamMember TeamMember { get; set; } = null!;

    public Status Status { get; set; } = Status.Compensated;

    /// <summary>Daily rate for this activity; null when not set yet (counts as 0).</summary>
    public decimal? DailyCompensation { get; set; }
}
