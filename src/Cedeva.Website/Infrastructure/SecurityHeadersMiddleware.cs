namespace Cedeva.Website.Infrastructure;

/// <summary>
/// Adds baseline security response headers and removes server fingerprinting headers.
/// The embeddable public-registration iframe is deliberately excluded from X-Frame-Options
/// so coordinators can still embed it on their own (third-party) sites.
/// </summary>
public class SecurityHeadersMiddleware
{
    private const string PublicRegistrationPath = "/PublicRegistration";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task Invoke(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Clickjacking protection for the authenticated app, but leave the embeddable
        // registration iframe framable by partner sites.
        if (!context.Request.Path.StartsWithSegments(PublicRegistrationPath))
        {
            headers["X-Frame-Options"] = "SAMEORIGIN";
        }

        return _next(context);
    }
}
