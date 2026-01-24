using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Presence.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Website.Features.Presence;

[Authorize]
public class PresenceController : Controller
{
    private readonly CedevaDbContext _context;

    public PresenceController(CedevaDbContext context)
    {
        _context = context;
    }

    // GET: Presence
    public async Task<IActionResult> Index()
    {
        var activities = await _context.Activities
            .Where(a => a.IsActive)
            .OrderBy(a => a.StartDate)
            .ToListAsync();

        var viewModel = new SelectActivityViewModel
        {
            Activities = activities
        };

        return View(viewModel);
    }

    // GET: Presence/SelectDay/5
    public async Task<IActionResult> SelectDay(int id)
    {
        var activity = await _context.Activities
            .Include(a => a.Days)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound();
        }

        var viewModel = new SelectDayViewModel
        {
            Activity = activity,
            ActivityDays = activity.Days
                .Where(d => d.IsActive)
                .OrderBy(d => d.DayDate)
                .ToList()
        };

        return View(viewModel);
    }

    // GET: Presence/List/5/10
    public async Task<IActionResult> List(int activityId, int dayId)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId);

        var activityDay = await _context.ActivityDays
            .FirstOrDefaultAsync(d => d.DayId == dayId);

        if (activity == null || activityDay == null)
        {
            return NotFound();
        }

        // Get all bookings for this activity with their booking days
        var bookings = await _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Group)
            .Include(b => b.Days)
            .Where(b => b.ActivityId == activityId)
            .ToListAsync();

        var presenceItems = new List<PresenceItemViewModel>();

        foreach (var booking in bookings)
        {
            // Get or create BookingDay for this activity day
            var bookingDay = booking.Days.FirstOrDefault(bd => bd.ActivityDayId == dayId);

            if (bookingDay == null)
            {
                // Create a new BookingDay if it doesn't exist
                bookingDay = new BookingDay
                {
                    ActivityDayId = dayId,
                    BookingId = booking.Id,
                    IsReserved = true,
                    IsPresent = false
                };
                _context.BookingDays.Add(bookingDay);
                await _context.SaveChangesAsync();
            }

            presenceItems.Add(new PresenceItemViewModel
            {
                BookingDayId = bookingDay.Id,
                BookingId = booking.Id,
                ChildFirstName = booking.Child.FirstName,
                ChildLastName = booking.Child.LastName,
                ChildBirthDate = booking.Child.BirthDate,
                ParentName = $"{booking.Child.Parent.FirstName} {booking.Child.Parent.LastName}",
                ParentPhone = booking.Child.Parent.MobilePhoneNumber ?? booking.Child.Parent.PhoneNumber ?? "",
                IsReserved = bookingDay.IsReserved,
                IsPresent = bookingDay.IsPresent,
                ActivityGroupName = booking.Group?.Label
            });
        }

        var viewModel = new PresenceListViewModel
        {
            Activity = activity,
            ActivityDay = activityDay,
            PresenceItems = presenceItems
                .OrderBy(p => p.ActivityGroupName)
                .ThenBy(p => p.ChildLastName)
                .ThenBy(p => p.ChildFirstName)
                .ToList()
        };

        return View(viewModel);
    }

    // POST: Presence/UpdatePresence
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePresence(int activityId, int dayId, Dictionary<int, bool> presence)
    {
        foreach (var kvp in presence)
        {
            var bookingDayId = kvp.Key;
            var isPresent = kvp.Value;

            var bookingDay = await _context.BookingDays.FindAsync(bookingDayId);
            if (bookingDay != null)
            {
                bookingDay.IsPresent = isPresent;
            }
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Présences enregistrées avec succès.";
        return RedirectToAction(nameof(List), new { activityId, dayId });
    }

    // GET: Presence/Print/5/10
    public async Task<IActionResult> Print(int activityId, int dayId)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId);

        var activityDay = await _context.ActivityDays
            .FirstOrDefaultAsync(d => d.DayId == dayId);

        if (activity == null || activityDay == null)
        {
            return NotFound();
        }

        var bookings = await _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Group)
            .Include(b => b.Days)
            .Where(b => b.ActivityId == activityId)
            .ToListAsync();

        var presenceItems = new List<PresenceItemViewModel>();

        foreach (var booking in bookings)
        {
            var bookingDay = booking.Days.FirstOrDefault(bd => bd.ActivityDayId == dayId);

            if (bookingDay != null && bookingDay.IsReserved)
            {
                presenceItems.Add(new PresenceItemViewModel
                {
                    BookingDayId = bookingDay.Id,
                    BookingId = booking.Id,
                    ChildFirstName = booking.Child.FirstName,
                    ChildLastName = booking.Child.LastName,
                    ChildBirthDate = booking.Child.BirthDate,
                    ParentName = $"{booking.Child.Parent.FirstName} {booking.Child.Parent.LastName}",
                    ParentPhone = booking.Child.Parent.MobilePhoneNumber ?? booking.Child.Parent.PhoneNumber ?? "",
                    IsReserved = bookingDay.IsReserved,
                    IsPresent = bookingDay.IsPresent,
                    ActivityGroupName = booking.Group?.Label
                });
            }
        }

        var viewModel = new PresenceListViewModel
        {
            Activity = activity,
            ActivityDay = activityDay,
            PresenceItems = presenceItems
                .OrderBy(p => p.ActivityGroupName)
                .ThenBy(p => p.ChildLastName)
                .ThenBy(p => p.ChildFirstName)
                .ToList()
        };

        return View(viewModel);
    }
}
