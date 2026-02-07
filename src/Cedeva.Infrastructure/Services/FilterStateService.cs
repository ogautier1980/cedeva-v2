using Cedeva.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Cedeva.Infrastructure.Services;

/// <summary>
/// Service for managing filter state in session and cookies.
/// Keeps URLs clean while maintaining filter persistence.
/// </summary>
public class FilterStateService : IFilterStateService
{
    private const string SessionKeyPrefix = "Filter_";
    private const string CookieKeyPrefix = "Filter_";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FilterStateService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public string? GetFilter(string key)
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

    public void SetFilter(string key, string? value)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        var sessionKey = SessionKeyPrefix + key;
        var cookieKey = CookieKeyPrefix + key;

        if (string.IsNullOrWhiteSpace(value))
        {
            // Clear if value is null/empty
            ClearFilter(key);
            return;
        }

        // Store in session (temporary)
        httpContext.Session.SetString(sessionKey, value);

        // Store in cookie (persistent, 30 days)
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

    public void ClearFilter(string key)
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

    public void ClearAllFilters(string controllerName)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        // Get all session keys for this controller
        var prefix = $"{SessionKeyPrefix}{controllerName}_";

        // Clear from session (note: Session doesn't provide key enumeration, so we clear known keys)
        // Controllers should call ClearFilter for each known filter key
    }

    public T? GetFilter<T>(string key) where T : struct
    {
        var value = GetFilter(key);
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

    public void SetFilter<T>(string key, T? value) where T : struct
    {
        if (!value.HasValue)
        {
            ClearFilter(key);
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

        SetFilter(key, stringValue);
    }
}
