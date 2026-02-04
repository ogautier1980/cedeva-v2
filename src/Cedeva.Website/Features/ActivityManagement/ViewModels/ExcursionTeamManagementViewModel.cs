using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class ExcursionTeamManagementViewModel
{
    public Excursion Excursion { get; set; } = null!;
    public Activity Activity { get; set; } = null!;

    /// <summary>
    /// All team members assigned to the parent activity, enriched with excursion assignment state.
    /// </summary>
    public List<ExcursionTeamMemberInfo> TeamMembers { get; set; } = new();
}

public class ExcursionTeamMemberInfo
{
    public int TeamMemberId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Whether this member is currently assigned to the excursion.</summary>
    public bool IsAssigned { get; set; }

    /// <summary>Whether this member was present at the excursion (only meaningful if assigned).</summary>
    public bool IsPresent { get; set; }

    /// <summary>PK of the ExcursionTeamMember record (null if not assigned).</summary>
    public int? ExcursionTeamMemberId { get; set; }
}
