using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.ActivityGroups.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.ActivityGroups;

[Authorize]
public class ActivityGroupsController : Controller
{
        
    private const string SessionKeyActivityId = "ActivityId";

    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ISessionStateService _sessionState;

    public ActivityGroupsController(
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IStringLocalizer<SharedResources> localizer,
        ISessionStateService sessionState)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _sessionState = sessionState;
    }

    // GET: ActivityGroups?activityId=5
    public async Task<IActionResult> Index(int? activityId)
    {
        // Check if any query parameters were provided in the actual HTTP request
        bool hasQueryParams = Request.Query.Count > 0;

        // If query params provided, store them and redirect to clean URL
        if (hasQueryParams)
        {
            if (activityId.HasValue)
                _sessionState.Set<int>(SessionKeyActivityId, activityId.Value); // ActivityId is context, persists to cookie

            // Redirect to clean URL
            return RedirectToAction(nameof(Index));
        }

        // Load activityId from service (no filters to clear - ActivityId is context, not a filter)
        activityId = _sessionState.Get<int>(SessionKeyActivityId);

        var query = _context.ActivityGroups
            .Include(g => g.Activity)
            .Include(g => g.Bookings)
            .AsQueryable();

        if (activityId.HasValue)
        {
            query = query.Where(g => g.ActivityId == activityId.Value);
            var activity = await _context.Activities.FindAsync(activityId.Value);
            if (activity != null)
            {
                this.SetActivityViewData(activityId.Value, activity.Name);
            }
        }

        var groups = await query
            .OrderBy(g => g.Activity != null ? g.Activity.Name : "")
            .ThenBy(g => g.Label)
            .Select(g => new ActivityGroupViewModel
            {
                Id = g.Id,
                Label = g.Label,
                Capacity = g.Capacity,
                ActivityId = g.ActivityId ?? 0,
                ActivityName = g.Activity != null ? g.Activity.Name : "",
                BookingsCount = g.Bookings.Count
            })
            .ToListAsync();

        return View(groups);
    }

    // GET: ActivityGroups/Create?activityId=5
    public async Task<IActionResult> Create(int? activityId)
    {
        await PopulateActivitiesDropdown(activityId);

        var model = new ActivityGroupViewModel();
        if (activityId.HasValue)
        {
            model.ActivityId = activityId.Value;
        }

        return View(model);
    }

    // POST: ActivityGroups/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityGroupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateActivitiesDropdown(model.ActivityId);
            return View(model);
        }

        var group = new ActivityGroup
        {
            Label = model.Label,
            Capacity = model.Capacity,
            ActivityId = model.ActivityId
        };

        await _context.ActivityGroups.AddAsync(group);
        await _unitOfWork.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["ActivityGroups.CreateSuccess"].ToString();

        // ActivityId is already in session
        return RedirectToAction(nameof(Index));
    }

    // GET: ActivityGroups/Edit/5
    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var group = await _context.ActivityGroups
            .Include(g => g.Activity)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null)
        {
            return NotFound();
        }

        var model = new ActivityGroupViewModel
        {
            Id = group.Id,
            Label = group.Label,
            Capacity = group.Capacity,
            ActivityId = group.ActivityId ?? 0,
            ActivityName = group.Activity?.Name
        };

        await PopulateActivitiesDropdown(model.ActivityId);
        ViewData["ReturnUrl"] = returnUrl;

        return View(model);
    }

    // POST: ActivityGroups/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityGroupViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateActivitiesDropdown(model.ActivityId);
            return View(model);
        }

        var group = await _context.ActivityGroups.FindAsync(id);
        if (group == null)
        {
            return NotFound();
        }

        group.Label = model.Label;
        group.Capacity = model.Capacity;
        group.ActivityId = model.ActivityId;

        _context.ActivityGroups.Update(group);
        await _unitOfWork.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["ActivityGroups.UpdateSuccess"].ToString();

        return this.RedirectToReturnUrlOrAction(returnUrl, nameof(Index));
    }

    // GET: ActivityGroups/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var group = await _context.ActivityGroups
            .Include(g => g.Activity)
            .Include(g => g.Bookings)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null)
        {
            return NotFound();
        }

        var model = new ActivityGroupViewModel
        {
            Id = group.Id,
            Label = group.Label,
            Capacity = group.Capacity,
            ActivityId = group.ActivityId ?? 0,
            ActivityName = group.Activity?.Name,
            BookingsCount = group.Bookings.Count
        };

        return View(model);
    }

    // POST: ActivityGroups/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var group = await _context.ActivityGroups
            .Include(g => g.Bookings)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null)
        {
            return NotFound();
        }

        // Check if group has bookings
        if (group.Bookings.Any())
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["ActivityGroups.DeleteErrorHasBookings"].Value;
            return RedirectToAction(nameof(Delete), new { id });
        }

        _context.ActivityGroups.Remove(group);
        await _unitOfWork.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["ActivityGroups.DeleteSuccess"].ToString();

        // ActivityId is already in session
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateActivitiesDropdown(int? selectedActivityId = null)
    {
        var activities = await _context.Activities
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name })
            .ToListAsync();

        ViewBag.Activities = new SelectList(activities, "Id", "Name", selectedActivityId);
    }
}
