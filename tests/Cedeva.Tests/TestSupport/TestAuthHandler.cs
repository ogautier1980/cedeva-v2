using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cedeva.Tests.TestSupport;

/// <summary>
/// Authenticates a request only when the <c>X-Test-User</c> header is present, emitting the
/// same claims the real app uses (NameIdentifier, OrganisationId, Role) so that
/// CurrentUserService resolves them. Header format: "userId|organisationId|role".
/// Without the header the request is treated as unauthenticated.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var parts = raw.ToString().Split('|');
        var userId = parts[0];

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId),
        };
        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            claims.Add(new Claim("OrganisationId", parts[1]));
        if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
        {
            claims.Add(new Claim("Role", parts[2]));        // read by CurrentUserService
            claims.Add(new Claim(ClaimTypes.Role, parts[2])); // used by [Authorize(Roles = ...)]
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
