namespace Cedeva.Website.Infrastructure;

/// <summary>
/// Adds baseline security response headers (incl. a Content-Security-Policy) and removes server
/// fingerprinting headers. The embeddable public-registration iframe is deliberately excluded from
/// X-Frame-Options and gets a permissive <c>frame-ancestors</c> so partners can still embed it.
///
/// The CSP allows the CDNs the app actually uses (jQuery, Bootstrap, Font Awesome, Choices.js,
/// TinyMCE) plus 'unsafe-inline' (the views use inline scripts/styles). Because a content CSP can
/// break a page if a resource was missed, it can be switched to report-only via configuration
/// <c>Security:ContentSecurityPolicyReportOnly = true</c> (escape hatch) while validating in a browser.
/// </summary>
public class SecurityHeadersMiddleware
{
    private const string PublicRegistrationPath = "/PublicRegistration";

    private readonly RequestDelegate _next;
    private readonly bool _cspReportOnly;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _cspReportOnly = configuration.GetValue("Security:ContentSecurityPolicyReportOnly", false);
    }

    public Task Invoke(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // The public-registration form must remain framable by partner sites; everything else is
        // protected from clickjacking (X-Frame-Options + CSP frame-ancestors).
        var isPublicForm = context.Request.Path.StartsWithSegments(PublicRegistrationPath);
        if (!isPublicForm)
        {
            headers["X-Frame-Options"] = "SAMEORIGIN";
        }

        var cspHeaderName = _cspReportOnly
            ? "Content-Security-Policy-Report-Only"
            : "Content-Security-Policy";
        headers[cspHeaderName] = BuildCsp(frameAncestors: isPublicForm ? "*" : "'self'");

        return _next(context);
    }

    private static string BuildCsp(string frameAncestors) => string.Join("; ",
        "default-src 'self'",
        "base-uri 'self'",
        "object-src 'none'",
        "frame-src 'self'",
        "img-src 'self' data: https:",
        "font-src 'self' data: https://cdnjs.cloudflare.com https://cdn.jsdelivr.net",
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://code.jquery.com https://cdn.tiny.cloud",
        "script-src 'self' 'unsafe-inline' https://code.jquery.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://cdn.tiny.cloud",
        "connect-src 'self' https://cdn.tiny.cloud",
        "form-action 'self'",
        $"frame-ancestors {frameAncestors}");
}
