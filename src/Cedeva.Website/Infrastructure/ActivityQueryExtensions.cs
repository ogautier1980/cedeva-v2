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
            .Include(a => a.TeamMembers);
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
}
