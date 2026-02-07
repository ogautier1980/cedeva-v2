namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service for managing filter state across user sessions.
/// Stores filter parameters in session/cookie to keep URLs clean.
/// </summary>
public interface IFilterStateService
{
    /// <summary>
    /// Gets a filter value by key from session or cookie.
    /// </summary>
    string? GetFilter(string key);

    /// <summary>
    /// Sets a filter value by key in session and cookie.
    /// </summary>
    void SetFilter(string key, string? value);

    /// <summary>
    /// Clears a specific filter by key.
    /// </summary>
    void ClearFilter(string key);

    /// <summary>
    /// Clears all filters for the current controller.
    /// </summary>
    void ClearAllFilters(string controllerName);

    /// <summary>
    /// Gets typed filter value.
    /// </summary>
    T? GetFilter<T>(string key) where T : struct;

    /// <summary>
    /// Sets typed filter value.
    /// </summary>
    void SetFilter<T>(string key, T? value) where T : struct;
}
