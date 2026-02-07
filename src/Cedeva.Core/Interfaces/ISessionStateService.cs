namespace Cedeva.Core.Interfaces;

/// <summary>
/// Generic service for managing state across user sessions.
/// Stores values in session (temporary) and cookies (persistent between sessions).
/// Keeps URLs clean by avoiding query parameters.
/// </summary>
public interface ISessionStateService
{
    /// <summary>
    /// Gets a string value by key from session or cookie.
    /// </summary>
    string? Get(string key);

    /// <summary>
    /// Gets a typed value by key from session or cookie.
    /// Supports int, bool, DateTime, decimal.
    /// </summary>
    T? Get<T>(string key) where T : struct;

    /// <summary>
    /// Sets a string value by key in session and optionally cookie.
    /// </summary>
    void Set(string key, string? value, bool persistToCookie = true);

    /// <summary>
    /// Sets a typed value by key in session and optionally cookie.
    /// Supports int, bool, DateTime, decimal.
    /// </summary>
    void Set<T>(string key, T? value, bool persistToCookie = true) where T : struct;

    /// <summary>
    /// Clears a specific value by key.
    /// </summary>
    void Clear(string key);

    /// <summary>
    /// Clears all values with a specific prefix.
    /// </summary>
    void ClearAllWithPrefix(string prefix);
}
