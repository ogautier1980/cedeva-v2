namespace Cedeva.Core.Entities;

/// <summary>
/// Base entity providing audit trail fields for tracking creation and modification.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>
    /// Date and time when the entity was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// User ID who created the entity. "System" for automated/unauthenticated operations.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date and time when the entity was last modified (UTC). Null if never modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// User ID who last modified the entity. Null if never modified.
    /// </summary>
    public string? ModifiedBy { get; set; }
}
