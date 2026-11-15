using Cedeva.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Website.Infrastructure;

public static class ActivityQueryExtensions
{
    /// <summary>
    /// Includes all related entities for Activity: Organisation, Bookings, Groups, TeamMembers
    /// </summary>
    public static IQueryable<Activity> IncludeAll(this IQueryable<Activity> query)
    {
        return query
            .Include(a => a.Organisation)
            .Include(a => a.Bookings)
            .Include(a => a.Groups)
            .Include(a => a.TeamMembers);
    }

    /// <summary>
    /// Includes all related entities plus Days for Activity
    /// </summary>
    public static IQueryable<Activity> IncludeAllWithDays(this IQueryable<Activity> query)
    {
        return query
            .Include(a => a.Days)
            .Include(a => a.Organisation)
            .Include(a => a.Bookings)
            .Include(a => a.Groups)
            .Include(a => a.TeamMembers)
            .Include(a => a.Address);
    }

    /// <summary>
    /// Includes minimal related entities: Organisation and Bookings
    /// </summary>
    public static IQueryable<Activity> IncludeBasic(this IQueryable<Activity> query)
    {
        return query
            .Include(a => a.Organisation)
            .Include(a => a.Bookings);
    }

    /// <summary>
    /// Case-insensitive, trimmed uniqueness check scoped to one organisation (a name can be reused
    /// across different organisations without issue), excluding the activity being edited if any.
    /// Query filters are bypassed so an admin checks against the target organisation.
    /// </summary>
    public static Task<bool> NameExistsInOrganisationAsync(this IQueryable<Activity> query,
        int organisationId, string name, int excludeActivityId = 0)
    {
        var trimmedLower = name.Trim().ToLowerInvariant();
        return query
            .IgnoreQueryFilters()
            // CA1862's string.Equals(..., OrdinalIgnoreCase) has no SQL translation in EF Core;
            // ToLower() becomes lower() on both PostgreSQL and SQLite.
#pragma warning disable CA1862
            .AnyAsync(a => a.OrganisationId == organisationId
                && a.Id != excludeActivityId
                && a.Name.ToLower() == trimmedLower);
#pragma warning restore CA1862
    }
}
