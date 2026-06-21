using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// A manageable expense category for an organisation (e.g. "Alimentation", "Transport"). Replaces
/// the previous free-text category: expenses still store the category name, but the set of names is
/// curated per organisation and offered as a dropdown (with add-on-the-fly).
/// </summary>
public class ExpenseCategory : AuditableEntity
{
    public int Id { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(50, ErrorMessage = "Validation.StringLength")]
    public string Name { get; set; } = string.Empty;
}
