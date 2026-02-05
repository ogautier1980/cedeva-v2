using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

/// <summary>
/// Reusable email template with variable support
/// </summary>
public class EmailTemplate
{
    public int Id { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(200, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Name { get; set; } = string.Empty;

    public EmailTemplateType TemplateType { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(10000, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string HtmlContent { get; set; } = string.Empty;

    /// <summary>
    /// Template par défaut pour ce type dans cette organisation
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Partagé dans toute l'organisation (visible par tous les coordinateurs)
    /// </summary>
    public bool IsShared { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public string CreatedByUserId { get; set; } = string.Empty;
    public CedevaUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
