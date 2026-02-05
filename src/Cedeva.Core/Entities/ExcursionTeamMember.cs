using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// Assignation d'un membre d'équipe à une excursion (accompagnateur)
/// </summary>
public class ExcursionTeamMember
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public int ExcursionId { get; set; }
    public Excursion Excursion { get; set; } = null!;

    [Required(ErrorMessage = "The {0} field is required.")]
    public int TeamMemberId { get; set; }
    public TeamMember TeamMember { get; set; } = null!;

    /// <summary>
    /// Prévu pour accompagner l'excursion
    /// </summary>
    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsAssigned { get; set; } = true;

    /// <summary>
    /// Présent effectivement à l'excursion
    /// </summary>
    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsPresent { get; set; } = false;

    /// <summary>
    /// Notes spécifiques (rôle, responsabilités, etc.)
    /// </summary>
    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? Notes { get; set; }
}
