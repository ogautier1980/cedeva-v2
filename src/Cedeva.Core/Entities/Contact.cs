using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// A free-form "other contact" of an organisation (e.g. a doctor, caterer, external partner) that
/// is not an app user, team member or parent. Shown in the Contacts page under "Autres contacts"
/// and selectable as an email recipient.
/// </summary>
public class Contact : AuditableEntity
{
    public int Id { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Validation.InvalidEmail")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    public string? Email { get; set; }

    [StringLength(50, ErrorMessage = "Validation.StringLength")]
    public string? PhoneNumber { get; set; }

    /// <summary>Free-text role/label (e.g. "Médecin", "Traiteur").</summary>
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string? Function { get; set; }

    public string FullName => $"{LastName}, {FirstName}";
}
