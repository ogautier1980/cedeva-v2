using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// Assignation d'un membre d'équipe à une excursion (accompagnateur)
/// </summary>
public class ExcursionTeamMember
{
    public int Id { get; set; }

    [Required]
    public int ExcursionId { get; set; }
    public Excursion Excursion { get; set; } = null!;

    [Required]
    public int TeamMemberId { get; set; }
    public TeamMember TeamMember { get; set; } = null!;

    /// <summary>
    /// Prévu pour accompagner l'excursion
    /// </summary>
    [Required]
    public bool IsAssigned { get; set; } = true;

    /// <summary>
    /// Présent effectivement à l'excursion
    /// </summary>
    [Required]
    public bool IsPresent { get; set; } = false;

    /// <summary>
    /// Notes spécifiques (rôle, responsabilités, etc.)
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
