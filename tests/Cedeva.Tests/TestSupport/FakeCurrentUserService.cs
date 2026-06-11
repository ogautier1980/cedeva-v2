using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;

namespace Cedeva.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="ICurrentUserService"/>. Note: the DbContext multi-tenancy query
/// filters dereference this service during EF parameter extraction, so it must never be null.
/// </summary>
public sealed class FakeCurrentUserService : ICurrentUserService
{
    public string? UserId { get; init; } = "test-user";
    public int? OrganisationId { get; init; }
    public Role? Role { get; init; }
    public bool IsAdmin { get; init; }

    public static FakeCurrentUserService Admin() =>
        new() { IsAdmin = true, Role = Cedeva.Core.Enums.Role.Admin };

    public static FakeCurrentUserService Coordinator(int organisationId) =>
        new() { IsAdmin = false, Role = Cedeva.Core.Enums.Role.Coordinator, OrganisationId = organisationId };
}
