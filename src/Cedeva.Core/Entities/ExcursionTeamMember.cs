using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// Assignation d'un membre d'équipe à une excursion (accompagnateur)
/// </summary>
public class ExcursionTeamMember : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int ExcursionId { get; set; }
    public Excursion Excursion { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    public int TeamMemberId { get; set; }
    public TeamMember TeamMember { get; set; } = null!;

    /// <summary>
    /// Prévu pour accompagner l'excursion
    /// </summary>
    [Required(ErrorMessage = "Validation.Required")]
    public bool IsAssigned { get; set; } = true;

    /// <summary>
    /// Présent effectivement à l'excursion
    /// </summary>
    [Required(ErrorMessage = "Validation.Required")]
    public bool IsPresent { get; set; } = false;

    /// <summary>
    /// Notes spécifiques (rôle, responsabilités, etc.)
    /// </summary>
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string? Notes { get; set; }
}
