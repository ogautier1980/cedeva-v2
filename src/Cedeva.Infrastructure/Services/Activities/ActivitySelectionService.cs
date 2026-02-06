using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Cedeva.Infrastructure.Services.Activities;

/// <summary>
/// Service for managing activity selection across user sessions.
/// Uses session storage (temporary) and cookies (persistent between sessions).
/// </summary>
public class ActivitySelectionService : IActivitySelectionService
{
    private const string SessionKey = "Activity_Id";
    private const string CookieKey = "SelectedActivityId";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ActivitySelectionService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public int? GetSelectedActivityId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Try session first (faster)
        var sessionValue = httpContext.Session.GetString(SessionKey);
        if (!string.IsNullOrEmpty(sessionValue) && int.TryParse(sessionValue, out var sessionId))
        {
            return sessionId;
        }

        // Fall back to cookie (persistent)
        var cookieValue = httpContext.Request.Cookies[CookieKey];
        if (!string.IsNullOrEmpty(cookieValue) && int.TryParse(cookieValue, out var cookieId))
        {
            // Restore session from cookie
            httpContext.Session.SetString(SessionKey, cookieId.ToString());
            return cookieId;
        }

        return null;
    }

    public void SetSelectedActivityId(int activityId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        // Store in session (temporary)
        httpContext.Session.SetString(SessionKey, activityId.ToString());

        // Store in cookie (persistent, 30 days)
        httpContext.Response.Cookies.Append(
            CookieKey,
            activityId.ToString(),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax
            });
    }

    public void ClearSelectedActivityId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        // Clear session
        httpContext.Session.Remove(SessionKey);

        // Clear cookie
        httpContext.Response.Cookies.Delete(CookieKey);
    }
}
