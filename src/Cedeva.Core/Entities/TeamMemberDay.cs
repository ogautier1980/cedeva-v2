using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class TeamMemberDay : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int ActivityDayId { get; set; }
    public ActivityDay ActivityDay { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    public int TeamMemberId { get; set; }
    public TeamMember TeamMember { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    public bool IsPresent { get; set; }
}
