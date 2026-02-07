using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Cedeva.Infrastructure.Services;

/// <summary>
/// Generic service for managing state across user sessions.
/// Uses session storage (temporary) and cookies (persistent, 30 days).
/// Keeps URLs clean by storing filter parameters, navigation state, and other values.
/// </summary>
public class SessionStateService : ISessionStateService
{
    private const string SessionKeyPrefix = "State_";
    private const string CookieKeyPrefix = "State_";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionStateService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public string? Get(string key)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        var sessionKey = SessionKeyPrefix + key;
        var cookieKey = CookieKeyPrefix + key;

        // Try session first (faster)
        var sessionValue = httpContext.Session.GetString(sessionKey);
        if (!string.IsNullOrEmpty(sessionValue))
        {
            return sessionValue;
        }

        // Fall back to cookie (persistent)
        var cookieValue = httpContext.Request.Cookies[cookieKey];
        if (!string.IsNullOrEmpty(cookieValue))
        {
            // Restore session from cookie
            httpContext.Session.SetString(sessionKey, cookieValue);
            return cookieValue;
        }

        return null;
    }

    public void Set(string key, string? value, bool persistToCookie = true)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        var sessionKey = SessionKeyPrefix + key;
        var cookieKey = CookieKeyPrefix + key;

        if (string.IsNullOrWhiteSpace(value))
        {
            Clear(key);
            return;
        }

        // Store in session (always)
        httpContext.Session.SetString(sessionKey, value);

        // Store in cookie only if persistence requested (for ActivityId, ReturnUrl, etc.)
        if (persistToCookie)
        {
            httpContext.Response.Cookies.Append(
                cookieKey,
                value,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                });
        }
    }

    public void Clear(string key)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        var sessionKey = SessionKeyPrefix + key;
        var cookieKey = CookieKeyPrefix + key;

        // Clear session
        httpContext.Session.Remove(sessionKey);

        // Clear cookie
        httpContext.Response.Cookies.Delete(cookieKey);
    }

    public void ClearAllWithPrefix(string prefix)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        // Note: Session doesn't provide key enumeration
        // Controllers should call Clear for each known key
    }

    public T? Get<T>(string key) where T : struct
    {
        var value = Get(key);
        if (string.IsNullOrEmpty(value))
            return null;

        try
        {
            if (typeof(T) == typeof(int))
            {
                if (int.TryParse(value, out var intValue))
                    return (T)(object)intValue;
            }
            else if (typeof(T) == typeof(bool))
            {
                if (bool.TryParse(value, out var boolValue))
                    return (T)(object)boolValue;
            }
            else if (typeof(T) == typeof(DateTime))
            {
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                    return (T)(object)dateValue;
            }
            else if (typeof(T) == typeof(decimal))
            {
                if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
                    return (T)(object)decimalValue;
            }
        }
        catch
        {
            // Parsing failed, return null
        }

        return null;
    }

    public void Set<T>(string key, T? value, bool persistToCookie = true) where T : struct
    {
        if (!value.HasValue)
        {
            Clear(key);
            return;
        }

        string stringValue;

        if (typeof(T) == typeof(DateTime))
        {
            stringValue = ((DateTime)(object)value.Value).ToString("o", CultureInfo.InvariantCulture);
        }
        else if (typeof(T) == typeof(decimal))
        {
            stringValue = ((decimal)(object)value.Value).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            stringValue = value.Value.ToString() ?? string.Empty;
        }

        Set(key, stringValue, persistToCookie);
    }
}
