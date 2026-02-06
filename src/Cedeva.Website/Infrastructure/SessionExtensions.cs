using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Cedeva.Website.Infrastructure;

/// <summary>
/// Extension methods for strongly-typed session access.
/// Provides type-safe alternatives to string-based session keys.
/// </summary>
public static class SessionExtensions
{
    /// <summary>
    /// Sets a strongly-typed object in session using JSON serialization.
    /// </summary>
    /// <typeparam name="T">The type of object to store</typeparam>
    /// <param name="session">The session instance</param>
    /// <param name="key">The session key</param>
    /// <param name="value">The value to store</param>
    public static void SetObject<T>(this ISession session, string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        session.SetString(key, json);
    }

    /// <summary>
    /// Gets a strongly-typed object from session using JSON deserialization.
    /// </summary>
    /// <typeparam name="T">The type of object to retrieve</typeparam>
    /// <param name="session">The session instance</param>
    /// <param name="key">The session key</param>
    /// <returns>The deserialized object, or null if not found</returns>
    public static T? GetObject<T>(this ISession session, string key)
    {
        var json = session.GetString(key);
        return json == null ? default : JsonSerializer.Deserialize<T>(json);
    }
}

/// <summary>
/// Strongly-typed wrapper for session data.
/// Provides type-safe access to session values without magic strings.
/// </summary>
public class SessionState
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionState(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Gets or sets the selected activity ID for the current session.
    /// </summary>
    public int? SelectedActivityId
    {
        get
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return null;

            var value = session.GetString("Activity_Id");
            return !string.IsNullOrEmpty(value) && int.TryParse(value, out var id) ? id : null;
        }
        set
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return;

            if (value.HasValue)
                session.SetString("Activity_Id", value.Value.ToString());
            else
                session.Remove("Activity_Id");
        }
    }

    /// <summary>
    /// Gets or sets the selected organisation ID for the current session (admin only).
    /// </summary>
    public int? SelectedOrganisationId
    {
        get
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return null;

            var value = session.GetString("Organisation_Id");
            return !string.IsNullOrEmpty(value) && int.TryParse(value, out var id) ? id : null;
        }
        set
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return;

            if (value.HasValue)
                session.SetString("Organisation_Id", value.Value.ToString());
            else
                session.Remove("Organisation_Id");
        }
    }

    /// <summary>
    /// Clears all session data.
    /// </summary>
    public void Clear()
    {
        _httpContextAccessor.HttpContext?.Session.Clear();
    }
}
