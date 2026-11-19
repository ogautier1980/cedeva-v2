using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class TeamMembersViewModel
{
    public Activity Activity { get; set; } = null!;
    public IEnumerable<TeamMember> AssignedTeamMembers { get; set; } = new List<TeamMember>();
    public IEnumerable<TeamMember> AvailableTeamMembers { get; set; } = new List<TeamMember>();

    /// <summary>Status and daily rate of each assigned member for this activity, by TeamMemberId.</summary>
    public IReadOnlyDictionary<int, ActivityTeamMember> Assignments { get; set; } = new Dictionary<int, ActivityTeamMember>();
}
