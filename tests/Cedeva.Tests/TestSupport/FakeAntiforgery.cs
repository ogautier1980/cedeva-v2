using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Cedeva.Tests.TestSupport;

/// <summary>
/// No-op antiforgery so integration tests can POST to [ValidateAntiForgeryToken] actions
/// without round-tripping a token. Validation always succeeds.
/// </summary>
public sealed class FakeAntiforgery : IAntiforgery
{
    private static AntiforgeryTokenSet Tokens =>
        new("test-request-token", "test-cookie-token", "__RequestVerificationToken", "X-CSRF-TOKEN");

    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => Tokens;
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => Tokens;
    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
    public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
    public void SetCookieTokenAndHeader(HttpContext httpContext) { }
}
