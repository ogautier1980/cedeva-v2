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
}
