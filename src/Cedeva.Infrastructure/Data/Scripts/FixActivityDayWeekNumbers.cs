using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Scripts;

public static class FixActivityDayWeekNumbers
{
    public static async Task ExecuteAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CedevaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CedevaDbContext>>();

        logger.LogInformation("Starting ActivityDay Week number correction...");

        var activities = await context.Activities
            .IgnoreQueryFilters()  // Ignore multi-tenant filters to see all activities
            .Include(a => a.Days)
            .ToListAsync();

        var totalUpdated = 0;

        foreach (var activity in activities)
        {
            foreach (var day in activity.Days)
            {
                var newWeekNumber = CalculateWeekNumber(day.DayDate, activity.StartDate);

                if (day.Week != newWeekNumber)
                {
                    day.Week = newWeekNumber;
                    totalUpdated++;
                }
            }
        }

        if (totalUpdated > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Updated {TotalUpdated} ActivityDay week numbers.", totalUpdated);
        }
        else
        {
            logger.LogInformation("No ActivityDay week numbers needed updating.");
        }
    }

    private static int CalculateWeekNumber(DateTime date, DateTime startDate)
    {
        // Find the first Sunday on or after startDate
        var firstSunday = startDate;
        while (firstSunday.DayOfWeek != DayOfWeek.Sunday)
        {
            firstSunday = firstSunday.AddDays(1);
        }

        // If date is before or on first Sunday, it's week 1
        if (date <= firstSunday)
        {
            return 1;
        }

        // Calculate weeks from first Monday (day after first Sunday)
        var firstMonday = firstSunday.AddDays(1);
        var daysSinceFirstMonday = (date - firstMonday).Days;
        return (daysSinceFirstMonday / 7) + 2; // +2 because week 1 already happened
    }
}
