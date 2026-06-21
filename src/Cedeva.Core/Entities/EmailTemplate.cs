using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

using Cedeva.Core.Interfaces;

namespace Cedeva.Core.Entities;

/// <summary>
/// Reusable email template with variable support
/// </summary>
public class EmailTemplate : AuditableEntity, IOrganisationScoped
{
    public int Id { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    /// <summary>
    /// Activity this template belongs to. <c>null</c> means an organisation-level template (the
    /// shared library that is copied into each new activity).
    /// </summary>
    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    public string Name { get; set; } = string.Empty;

    public EmailTemplateType TemplateType { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(10000, ErrorMessage = "Validation.StringLength")]
    public string HtmlContent { get; set; } = string.Empty;

    /// <summary>
    /// Template par défaut pour ce type, dans son périmètre (activité si <see cref="ActivityId"/>
    /// est renseigné, sinon organisation).
    /// </summary>
    public bool IsDefault { get; set; }

    public CedevaUser? CreatedByUser { get; set; }
}
