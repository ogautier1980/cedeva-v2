namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service for retrieving user display information.
/// </summary>
public interface IUserDisplayService
{
    /// <summary>
    /// Gets a display name for a user ID. Returns "System" for system operations,
    /// full name for valid users, or the user ID if user not found.
    /// </summary>
    Task<string> GetUserDisplayNameAsync(string userId);

    /// <summary>
    /// Gets display names for multiple user IDs in a single query.
    /// Useful for batch operations to avoid N+1 queries.
    /// </summary>
    Task<Dictionary<string, string>> GetUserDisplayNamesAsync(IEnumerable<string> userIds);
}
