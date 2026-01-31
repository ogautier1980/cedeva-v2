using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Presence.ViewModels;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Presence;

[Authorize]
public class PresenceController : Controller
{
    private const string TempDataSuccessMessage = "SuccessMessage";
    private const string SessionKeyActivityId = "Presence_ActivityId";
    private const string SessionKeyDayId = "Presence_DayId";

    private readonly CedevaDbContext _context;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public PresenceController(CedevaDbContext context, IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _localizer = localizer;
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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var activity = await _context.Activities
            .Include(a => a.Days)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound();
        }

        // Auto-redirect to today's list if we're during the activity period
        var today = DateTime.Today;

        // Try to find today's activity day first, or the closest future active day
        var targetDay = activity.Days
            .Where(d => d.IsActive)
            .OrderBy(d => d.DayDate)
            .FirstOrDefault(d => d.DayDate.Date >= today);

        // If no future day found, try to get today's day even if in the past
        if (targetDay == null)
        {
            targetDay = activity.Days
                .FirstOrDefault(d => d.IsActive && d.DayDate.Date == today);
        }

        // Redirect if we found a valid day
        if (targetDay != null && today >= activity.StartDate.Date && today <= activity.EndDate.Date)
        {
            return RedirectToAction(nameof(List), new { activityId = activity.Id, dayId = targetDay.DayId });
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
    public async Task<IActionResult> List(int? activityId, int? dayId)
    {
        // Store in session if provided
        if (activityId.HasValue)
        {
            HttpContext.Session.SetInt32(SessionKeyActivityId, activityId.Value);
        }
        else
        {
            activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        }

        if (dayId.HasValue)
        {
            HttpContext.Session.SetInt32(SessionKeyDayId, dayId.Value);
        }
        else
        {
            dayId = HttpContext.Session.GetInt32(SessionKeyDayId);
        }

        if (!activityId.HasValue || !dayId.HasValue)
        {
            return BadRequest();
        }

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId.Value);

        var activityDay = await _context.ActivityDays
            .FirstOrDefaultAsync(d => d.DayId == dayId.Value);

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
            .Where(b => b.ActivityId == activityId.Value)
            .ToListAsync();

        var presenceItems = new List<PresenceItemViewModel>();

        foreach (var booking in bookings)
        {
            // Get or create BookingDay for this activity day
            var bookingDay = booking.Days.FirstOrDefault(bd => bd.ActivityDayId == dayId.Value);

            if (bookingDay == null)
            {
                // Create a new BookingDay if it doesn't exist
                bookingDay = new BookingDay
                {
                    ActivityDayId = dayId.Value,
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
    public async Task<IActionResult> UpdatePresence(Dictionary<int, bool> presence)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(List));
        }

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

        TempData[TempDataSuccessMessage] = _localizer["Message.PresencesSaved"].Value;
        return RedirectToAction(nameof(List));
    }

    // GET: Presence/Print
    public async Task<IActionResult> Print()
    {
        // Get from session
        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        var dayId = HttpContext.Session.GetInt32(SessionKeyDayId);

        if (!activityId.HasValue || !dayId.HasValue)
        {
            return BadRequest();
        }

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId.Value);

        var activityDay = await _context.ActivityDays
            .FirstOrDefaultAsync(d => d.DayId == dayId.Value);

        if (activity == null || activityDay == null)
        {
            return NotFound();
        }

        var bookings = await _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Group)
            .Include(b => b.Days)
            .Where(b => b.ActivityId == activityId.Value)
            .ToListAsync();

        var presenceItems = new List<PresenceItemViewModel>();

        foreach (var booking in bookings)
        {
            var bookingDay = booking.Days.FirstOrDefault(bd => bd.ActivityDayId == dayId.Value);

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
