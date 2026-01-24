using System.Security.Claims;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Cedeva.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public int? OrganisationId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("OrganisationId");
            return int.TryParse(claim, out var id) ? id : null;
        }
    }

    public Role? Role
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("Role");
            return Enum.TryParse<Role>(claim, out var role) ? role : null;
        }
    }

    public bool IsAdmin => Role == Core.Enums.Role.Admin;
}
