using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services;

/// <summary>
/// Service for retrieving user display information.
/// </summary>
public class UserDisplayService : IUserDisplayService
{
    private const string SystemUserIdentifier = "System";
    private readonly CedevaDbContext _context;

    public UserDisplayService(CedevaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets a display name for a user ID. Returns "System" for system operations,
    /// full name for valid users, or the user ID if user not found.
    /// </summary>
    public async Task<string> GetUserDisplayNameAsync(string userId)
    {
        if (userId == SystemUserIdentifier)
        {
            return SystemUserIdentifier;
        }

        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync();

        return user != null
            ? $"{user.FirstName} {user.LastName}".Trim()
            : userId;
    }

    /// <summary>
    /// Gets display names for multiple user IDs in a single query.
    /// </summary>
    public async Task<Dictionary<string, string>> GetUserDisplayNamesAsync(IEnumerable<string> userIds)
    {
        var distinctUserIds = userIds.Where(id => id != SystemUserIdentifier).Distinct().ToList();

        if (!distinctUserIds.Any())
        {
            return userIds.ToDictionary(id => id, _ => SystemUserIdentifier);
        }

        var users = await _context.Users
            .Where(u => distinctUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync();

        var result = users.ToDictionary(
            u => u.Id,
            u => $"{u.FirstName} {u.LastName}".Trim());

        // Add "System" entries and missing users
        var missingUserIds = userIds.Where(id => !result.ContainsKey(id));
        foreach (var userId in missingUserIds)
        {
            result[userId] = userId == SystemUserIdentifier ? SystemUserIdentifier : userId;
        }

        return result;
    }
}
