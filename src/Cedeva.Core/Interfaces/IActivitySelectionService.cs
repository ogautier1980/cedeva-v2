namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service for managing activity selection across user sessions.
/// Provides a centralized way to store and retrieve the currently selected activity
/// using both session (temporary) and cookie (persistent) storage.
/// </summary>
public interface IActivitySelectionService
{
    /// <summary>
    /// Gets the currently selected activity ID from session or cookie.
    /// Returns null if no activity is selected.
    /// </summary>
    int? GetSelectedActivityId();

    /// <summary>
    /// Sets the currently selected activity ID in both session and cookie.
    /// </summary>
    /// <param name="activityId">The ID of the activity to select.</param>
    void SetSelectedActivityId(int activityId);

    /// <summary>
    /// Clears the currently selected activity from session and cookie.
    /// </summary>
    void ClearSelectedActivityId();
}
