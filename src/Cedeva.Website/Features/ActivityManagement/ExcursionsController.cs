using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.ActivityManagement.ViewModels;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.ActivityManagement;

[Authorize]
public class ExcursionsController : Controller
{
    private const string SessionActivityId = "Activity_Id";
    private const string CookieActivityId = "SelectedActivityId";

    private readonly CedevaDbContext _context;
    private readonly IExcursionService _excursionService;
    private readonly ILogger<ExcursionsController> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ExcursionsController(
        CedevaDbContext context,
        IExcursionService excursionService,
        ILogger<ExcursionsController> logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _excursionService = excursionService;
        _logger = logger;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        var activityId = id ?? GetActivityIdFromSession();
        if (activityId == null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity == null)
            return NotFound();

        SetSelectedActivityId(activityId.Value);

        // Load excursions with related data
        var excursions = await _context.Excursions
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .Include(e => e.Registrations)
            .Include(e => e.Expenses)
            .Where(e => e.ActivityId == activityId && e.IsActive)
            .OrderBy(e => e.ExcursionDate)
            .ToListAsync();

        var excursionItems = excursions.Select(e => new ExcursionListItem
        {
            Id = e.Id,
            Name = e.Name,
            ExcursionDate = e.ExcursionDate,
            Type = _localizer[$"Enum.ExcursionType.{e.Type}"],
            Cost = e.Cost,
            IsActive = e.IsActive,
            TargetGroupNames = e.ExcursionGroups.Select(eg => eg.ActivityGroup.Label).ToList(),
            RegistrationCount = e.Registrations.Count,
            TotalRevenue = e.Registrations.Count * e.Cost,
            TotalExpenses = e.Expenses.Sum(ex => ex.Amount),
            NetBalance = (e.Registrations.Count * e.Cost) - e.Expenses.Sum(ex => ex.Amount)
        }).ToList();

        var viewModel = new ExcursionsIndexViewModel
        {
            Activity = activity,
            Excursions = excursionItems
        };

        ViewData["ActivityId"] = activity.Id;
        ViewData["ActivityName"] = activity.Name;

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? id)
    {
        var activityId = id ?? GetActivityIdFromSession();
        if (activityId == null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity == null)
            return NotFound();

        var viewModel = new CreateExcursionViewModel
        {
            ActivityId = activity.Id,
            Activity = activity,
            AvailableGroups = activity.Groups.ToList(),
            ExcursionDate = DateTime.Today
        };

        ViewData["ActivityId"] = activity.Id;
        ViewData["ActivityName"] = activity.Name;

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateExcursionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var activity = await _context.Activities
                .Include(a => a.Groups)
                .FirstOrDefaultAsync(a => a.Id == model.ActivityId);

            if (activity != null)
            {
                model.Activity = activity;
                model.AvailableGroups = activity.Groups.ToList();
                ViewData["ActivityId"] = activity.Id;
                ViewData["ActivityName"] = activity.Name;
            }

            return View(model);
        }

        // Validate that at least one group is selected
        if (model.SelectedGroupIds == null || model.SelectedGroupIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedGroupIds), _localizer["Validation.AtLeastOneGroupRequired"]);

            var activity = await _context.Activities
                .Include(a => a.Groups)
                .FirstOrDefaultAsync(a => a.Id == model.ActivityId);

            if (activity != null)
            {
                model.Activity = activity;
                model.AvailableGroups = activity.Groups.ToList();
                ViewData["ActivityId"] = activity.Id;
                ViewData["ActivityName"] = activity.Name;
            }

            return View(model);
        }

        // Create excursion
        var excursion = new Excursion
        {
            Name = model.Name,
            Description = model.Description,
            ExcursionDate = model.ExcursionDate,
            Cost = model.Cost,
            Type = model.Type,
            ActivityId = model.ActivityId,
            IsActive = true
        };

        _context.Excursions.Add(excursion);
        await _context.SaveChangesAsync();

        // Create ExcursionGroup links
        foreach (var groupId in model.SelectedGroupIds)
        {
            var excursionGroup = new ExcursionGroup
            {
                ExcursionId = excursion.Id,
                ActivityGroupId = groupId
            };
            _context.ExcursionGroups.Add(excursionGroup);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = _localizer["Message.ExcursionCreated"].ToString();
        return RedirectToAction(nameof(Index), new { id = model.ActivityId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult BeginExcursions(int id)
    {
        SetSelectedActivityId(id);
        return RedirectToAction(nameof(Index), new { id });
    }

    private int? GetActivityIdFromSession()
    {
        // Try session first
        var idStr = HttpContext.Session.GetString(SessionActivityId);

        // If not in session, try persistent cookie
        if (string.IsNullOrEmpty(idStr))
        {
            idStr = Request.Cookies[CookieActivityId];

            // Restore session from cookie
            if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var cookieParsed))
            {
                HttpContext.Session.SetString(SessionActivityId, cookieParsed.ToString());
                return cookieParsed;
            }
        }
        else if (int.TryParse(idStr, out var sessionParsed))
        {
            return sessionParsed;
        }

        return null;
    }

    private void SetSelectedActivityId(int activityId)
    {
        // Store in session for current session
        HttpContext.Session.SetString(SessionActivityId, activityId.ToString());

        // Store in persistent cookie (30 days)
        Response.Cookies.Append(CookieActivityId, activityId.ToString(), new CookieOptions
        {
            Expires = DateTimeOffset.Now.AddDays(30),
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });
    }
}
