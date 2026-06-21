using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

/// <summary>
/// Excursion liée à une activité
/// </summary>
public class Excursion : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public DateTime ExcursionDate { get; set; }

    /// <summary>
    /// Heure de début (optionnel, pour demi-journée ou plage horaire)
    /// </summary>
    public TimeSpan? StartTime { get; set; }

    /// <summary>
    /// Heure de fin (optionnel, pour demi-journée ou plage horaire)
    /// </summary>
    public TimeSpan? EndTime { get; set; }

    /// <summary>
    /// Coût par enfant
    /// </summary>
    [Required(ErrorMessage = "Validation.Required")]
    public decimal Cost { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public ExcursionType Type { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public bool IsActive { get; set; } = true;

    // Relationships
    [Required(ErrorMessage = "Validation.Required")]
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public ICollection<ExcursionGroup> ExcursionGroups { get; set; } = new List<ExcursionGroup>();
    public ICollection<ExcursionRegistration> Registrations { get; set; } = new List<ExcursionRegistration>();
    public ICollection<ExcursionTeamMember> TeamMembers { get; set; } = new List<ExcursionTeamMember>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
