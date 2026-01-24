using System.Security.Claims;
using Cedeva.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Cedeva.Infrastructure.Identity;

public class CedevaUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<CedevaUser, IdentityRole>
{
    public CedevaUserClaimsPrincipalFactory(
        UserManager<CedevaUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(CedevaUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Add custom claims
        identity.AddClaim(new Claim("Role", user.Role.ToString()));

        if (user.OrganisationId.HasValue)
        {
            identity.AddClaim(new Claim("OrganisationId", user.OrganisationId.Value.ToString()));
        }

        return identity;
    }
}
