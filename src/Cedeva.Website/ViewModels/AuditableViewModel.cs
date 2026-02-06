namespace Cedeva.Website.ViewModels;

/// <summary>
/// Base class for ViewModels that include audit trail information
/// </summary>
public abstract class AuditableViewModel
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }

    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ModifiedByDisplayName { get; set; }
}
