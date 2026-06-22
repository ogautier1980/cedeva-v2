using System.Globalization;
using Cedeva.Core.Entities;

namespace Cedeva.Core.Helpers;

/// <summary>
/// Generates the <see cref="ActivityDay"/> rows for an activity's date range and computes week
/// numbers. Shared by the activity editor (ActivitiesController) and the CSV importer so the rules
/// (one day per date, weekends inactive, week numbering) live in one place.
/// </summary>
public static class ActivityDayGenerator
{
    private const string DayLabelFormat = "dddd d MMMM";
    private static readonly CultureInfo Culture = new("fr-BE");

    /// <summary>Adds an active (weekday) / inactive (weekend) day for each date in the range.</summary>
    public static void GenerateDays(Activity activity)
    {
        for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
        {
            activity.Days.Add(new ActivityDay
            {
                Label = FormatLabel(date),
                DayDate = date,
                Week = GetWeekNumber(date, activity.StartDate),
                IsActive = !IsWeekend(date)
            });
        }
    }

    public static string FormatLabel(DateTime date) => date.ToString(DayLabelFormat, Culture);

    public static bool IsWeekend(DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>Week number within the activity (week 1 up to the first Sunday, then +1 per week).</summary>
    public static int GetWeekNumber(DateTime date, DateTime startDate)
    {
        var firstSunday = startDate;
        while (firstSunday.DayOfWeek != DayOfWeek.Sunday)
            firstSunday = firstSunday.AddDays(1);

        if (date <= firstSunday)
            return 1;

        var firstMonday = firstSunday.AddDays(1);
        return ((date - firstMonday).Days / 7) + 2;
    }
}
