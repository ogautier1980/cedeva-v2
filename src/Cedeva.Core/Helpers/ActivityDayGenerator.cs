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

    /// <summary>Adds any missing days for the current range as inactive (used when opening the editor).</summary>
    public static void EnsureAllDaysExist(Activity activity)
    {
        var existingDates = activity.Days.Select(d => d.DayDate.Date).ToHashSet();
        for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
        {
            if (!existingDates.Contains(date.Date))
            {
                activity.Days.Add(new ActivityDay
                {
                    Label = FormatLabel(date),
                    DayDate = date,
                    Week = GetWeekNumber(date, activity.StartDate),
                    IsActive = false
                });
            }
        }
    }

    /// <summary>
    /// Reconciles the day list after a date-range change: adds missing days at the (extended) edges,
    /// deactivates days now out of range, and recomputes week numbers. Does not touch bookings.
    /// </summary>
    public static void HandleDateRangeChanges(Activity activity, DateTime newStartDate, DateTime newEndDate, DateTime oldStartDate, DateTime oldEndDate)
    {
        var existingDates = activity.Days.Select(d => d.DayDate.Date).ToHashSet();

        if (newStartDate < oldStartDate)
        {
            for (var date = newStartDate; date < oldStartDate; date = date.AddDays(1))
                AddDayIfMissing(activity, existingDates, date, newStartDate);
        }

        if (newEndDate > oldEndDate)
        {
            for (var date = oldEndDate.AddDays(1); date <= newEndDate; date = date.AddDays(1))
                AddDayIfMissing(activity, existingDates, date, newStartDate);
        }

        foreach (var day in activity.Days.Where(d => d.DayDate < newStartDate || d.DayDate > newEndDate))
            day.IsActive = false;

        foreach (var day in activity.Days)
            day.Week = GetWeekNumber(day.DayDate, newStartDate);
    }

    private static void AddDayIfMissing(Activity activity, HashSet<DateTime> existingDates, DateTime date, DateTime weekStart)
    {
        if (existingDates.Contains(date.Date))
            return;
        activity.Days.Add(new ActivityDay
        {
            Label = FormatLabel(date),
            DayDate = date,
            Week = GetWeekNumber(date, weekStart),
            IsActive = !IsWeekend(date)
        });
    }

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
