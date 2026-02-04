using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

/// <summary>
/// Excursion liée à une activité
/// </summary>
public class Excursion
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
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
    [Required]
    public decimal Cost { get; set; }

    [Required]
    public ExcursionType Type { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    // Relationships
    [Required]
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public ICollection<ExcursionGroup> ExcursionGroups { get; set; } = new List<ExcursionGroup>();
    public ICollection<ExcursionRegistration> Registrations { get; set; } = new List<ExcursionRegistration>();
    public ICollection<ExcursionTeamMember> TeamMembers { get; set; } = new List<ExcursionTeamMember>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
