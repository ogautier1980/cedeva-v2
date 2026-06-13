using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Authenticates a request only when the <c>X-Test-User</c> header is present (format
/// "userId|organisationId|role"), emitting the claims CurrentUserService reads. Without the
/// header the request is anonymous — so the public iframe flow stays realistic while admin
/// pages can be driven by setting the header on a Playwright browser context.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var raw) || string.IsNullOrWhiteSpace(raw))
            return Task.FromResult(AuthenticateResult.NoResult());

        var parts = raw.ToString().Split('|');
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, parts[0]),
            new(ClaimTypes.Name, parts[0]),
        };
        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            claims.Add(new Claim("OrganisationId", parts[1]));
        if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
        {
            claims.Add(new Claim("Role", parts[2]));
            claims.Add(new Claim(ClaimTypes.Role, parts[2]));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
