using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;

namespace Cedeva.Core.Entities;

/// <summary>
/// Org-level default question, copied into a new <see cref="Activity"/>'s own
/// <see cref="ActivityQuestion"/> rows at creation time (Lot J — "modèle de questions par
/// activité"). Editing a template afterwards does not touch activities already created from it —
/// each activity's copy is independently editable, same as the Lot E locked email templates.
/// </summary>
public class OrganisationQuestionTemplate : AuditableEntity, IOrganisationScoped
{
    public int Id { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string QuestionText { get; set; } = string.Empty;

    public QuestionType QuestionType { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>Comma-separated options for dropdown/radio questions.</summary>
    public string? Options { get; set; }

    public int DisplayOrder { get; set; } = 1;
}
