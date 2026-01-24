using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class TeamMembersViewModel
{
    public Activity Activity { get; set; } = null!;
    public IEnumerable<TeamMember> AssignedTeamMembers { get; set; } = new List<TeamMember>();
    public IEnumerable<TeamMember> AvailableTeamMembers { get; set; } = new List<TeamMember>();
}
