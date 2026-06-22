using Cedeva.Core.Entities;
using Cedeva.Core.Helpers;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Activities;

/// <inheritdoc cref="IActivityDayService"/>
public class ActivityDayService : IActivityDayService
{
    private readonly CedevaDbContext _context;

    public ActivityDayService(CedevaDbContext context) => _context = context;

    public async Task<DayActivationResult> ApplyDayActivationChangesAsync(
        Activity activity, IReadOnlyList<int> activeDayIds, bool addDaysToBookings, bool removeDaysConfirmed, CancellationToken ct = default)
    {
        var currentlyActiveDayIds = activity.Days.Where(d => d.IsActive).Select(d => d.DayId).ToList();
        var daysBeingActivated = activeDayIds.Except(currentlyActiveDayIds).ToList();
        var daysBeingDeactivated = currentlyActiveDayIds.Except(activeDayIds).ToList();

        // --- Deactivation (with confirmation when reserved bookings are affected) ---
        if (daysBeingDeactivated.Count > 0)
        {
            var reserved = await _context.BookingDays
                .CountAsync(bd => daysBeingDeactivated.Contains(bd.ActivityDayId) && bd.IsReserved, ct);

            if (reserved > 0 && !removeDaysConfirmed)
            {
                var labels = activity.Days.Where(d => daysBeingDeactivated.Contains(d.DayId)).Select(d => d.Label).ToList();
                var remaining = activity.Days
                    .Where(d => d.IsActive && !daysBeingDeactivated.Contains(d.DayId))
                    .Select(d => d.DayId).ToList();
                return new DayActivationResult(DayActivationOutcome.NeedsRemoveConfirmation, reserved, labels, remaining);
            }

            foreach (var dayId in daysBeingDeactivated)
            {
                var day = activity.Days.FirstOrDefault(d => d.DayId == dayId);
                if (day == null) continue;
                day.IsActive = false;
                var bookingDays = await _context.BookingDays.Where(bd => bd.ActivityDayId == dayId).ToListAsync(ct);
                _context.BookingDays.RemoveRange(bookingDays);
            }
        }

        // --- Activation (optionally adding the day to existing bookings) ---
        if (daysBeingActivated.Count > 0)
        {
            foreach (var dayId in daysBeingActivated)
            {
                var day = activity.Days.FirstOrDefault(d => d.DayId == dayId);
                if (day == null) continue;
                day.IsActive = true;

                if (addDaysToBookings)
                {
                    foreach (var booking in activity.Bookings.Where(b => b.Days.All(bd => bd.ActivityDayId != dayId)))
                    {
                        booking.Days.Add(new BookingDay
                        {
                            BookingId = booking.Id,
                            ActivityDayId = dayId,
                            IsReserved = true,
                            IsPresent = false
                        });
                    }
                }
            }

            if (!addDaysToBookings && activity.Bookings.Any())
            {
                var labels = activity.Days.Where(d => daysBeingActivated.Contains(d.DayId)).Select(d => d.Label).ToList();
                return new DayActivationResult(DayActivationOutcome.NeedsActivateInfo, DayLabels: labels, ActivatedDayIds: daysBeingActivated);
            }
        }

        return new DayActivationResult(DayActivationOutcome.Applied);
    }

    public async Task<AdjustDaysResult> AdjustAsync(int activityId, string edge, string op, bool confirmed, CancellationToken ct = default)
    {
        var activity = await _context.Activities.Include(a => a.Days).FirstOrDefaultAsync(a => a.Id == activityId, ct);
        if (activity == null)
            return new AdjustDaysResult(AdjustDaysOutcome.NotFound);

        edge = (edge ?? string.Empty).ToLowerInvariant();
        op = (op ?? string.Empty).ToLowerInvariant();
        if (edge is not ("start" or "end") || op is not ("extend" or "shrink"))
            return new AdjustDaysResult(AdjustDaysOutcome.BadRequest);

        if (op == "extend")
        {
            var newDate = edge == "start" ? activity.StartDate.AddDays(-1) : activity.EndDate.AddDays(1);
            var existing = activity.Days.FirstOrDefault(d => d.DayDate.Date == newDate.Date);
            if (existing != null)
                existing.IsActive = true;
            else
                activity.Days.Add(new ActivityDay { Label = ActivityDayGenerator.FormatLabel(newDate), DayDate = newDate, IsActive = true, Week = 0 });

            if (edge == "start") activity.StartDate = newDate; else activity.EndDate = newDate;
        }
        else // shrink
        {
            var activeDays = activity.Days.Where(d => d.IsActive).OrderBy(d => d.DayDate).ToList();
            if (activeDays.Count <= 1)
                return new AdjustDaysResult(AdjustDaysOutcome.CannotRemoveLastDay);

            var edgeDay = edge == "start" ? activeDays[0] : activeDays[^1];

            var reservedCount = await _context.BookingDays.CountAsync(bd => bd.ActivityDayId == edgeDay.DayId && bd.IsReserved, ct);
            if (reservedCount > 0 && !confirmed)
                return new AdjustDaysResult(AdjustDaysOutcome.NeedsConfirmation, reservedCount, edgeDay.Label);

            // Decrement each affected booking's total by one PricePerDay (not a full recompute, so
            // excursion costs already in the total are preserved), then drop the BookingDays.
            var price = activity.PricePerDay ?? 0m;
            var bookingDays = await _context.BookingDays
                .Include(bd => bd.Booking)
                .Where(bd => bd.ActivityDayId == edgeDay.DayId)
                .ToListAsync(ct);

            foreach (var bd in bookingDays.Where(bd => bd.IsReserved && bd.Booking != null))
                bd.Booking.TotalAmount = Math.Max(0m, bd.Booking.TotalAmount - price);
            _context.BookingDays.RemoveRange(bookingDays);

            edgeDay.IsActive = false;

            var remaining = activity.Days.Where(d => d.IsActive).OrderBy(d => d.DayDate).ToList();
            activity.StartDate = remaining[0].DayDate;
            activity.EndDate = remaining[^1].DayDate;
        }

        foreach (var d in activity.Days)
            d.Week = ActivityDayGenerator.GetWeekNumber(d.DayDate, activity.StartDate);

        await _context.SaveChangesAsync(ct);

        var days = activity.Days.OrderBy(d => d.DayDate)
            .Select(d => new AdjustDayDto(d.DayId, d.Label, d.DayDate.ToString("yyyy-MM-dd"), d.IsActive, d.Week ?? 0))
            .ToList();

        return new AdjustDaysResult(
            AdjustDaysOutcome.Success,
            StartDate: activity.StartDate.ToString("yyyy-MM-dd"),
            EndDate: activity.EndDate.ToString("yyyy-MM-dd"),
            ActiveDaysCount: activity.Days.Count(d => d.IsActive),
            Days: days);
    }
}
