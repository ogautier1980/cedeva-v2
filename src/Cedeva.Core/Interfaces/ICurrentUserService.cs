using Cedeva.Core.Enums;

namespace Cedeva.Core.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    int? OrganisationId { get; }
    Role? Role { get; }
    bool IsAdmin { get; }
}
