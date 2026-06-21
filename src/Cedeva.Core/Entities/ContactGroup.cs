using System.ComponentModel.DataAnnotations;

using Cedeva.Core.Interfaces;

namespace Cedeva.Core.Entities;

/// <summary>
/// A named, reusable group of email recipients built from an organisation's contacts (parents,
/// team members, other contacts). Used as a recipient when sending emails.
/// </summary>
public class ContactGroup : AuditableEntity, IOrganisationScoped
{
    public int Id { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string Name { get; set; } = string.Empty;

    public ICollection<ContactGroupMember> Members { get; set; } = new List<ContactGroupMember>();
}

/// <summary>A single recipient (email snapshot) belonging to a <see cref="ContactGroup"/>.</summary>
public class ContactGroupMember
{
    public int Id { get; set; }

    public int ContactGroupId { get; set; }
    public ContactGroup ContactGroup { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [StringLength(200)]
    public string? DisplayName { get; set; }
}
