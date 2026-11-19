using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Microsoft.AspNetCore.Identity;

namespace Cedeva.Core.Entities;

public class CedevaUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public int? OrganisationId { get; set; }
    public Organisation? Organisation { get; set; }

    public Role Role { get; set; } = Role.Coordinator;

    /// <summary>Interface language chosen on the profile ("fr", "nl", "en"); null = default (fr).</summary>
    [MaxLength(5)]
    public string? PreferredLanguage { get; set; }

    // Audit fields (cannot inherit from AuditableEntity - already inherits from IdentityUser)
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}
