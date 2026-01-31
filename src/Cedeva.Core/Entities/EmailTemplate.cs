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

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public EmailTemplateType TemplateType { get; set; }

    [Required]
    [StringLength(500)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(10000)]
    public string HtmlContent { get; set; } = string.Empty;

    /// <summary>
    /// Template par défaut pour ce type dans cette organisation
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Partagé dans toute l'organisation (visible par tous les coordinateurs)
    /// </summary>
    public bool IsShared { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;
    public CedevaUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
