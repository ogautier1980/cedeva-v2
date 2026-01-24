using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Activities.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Website.Features.Activities;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ActivitiesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? searchTerm, bool? showActiveOnly, int page = 1)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var query = _context.Activities
            .Include(a => a.Organisation)
            .Include(a => a.Bookings)
            .Include(a => a.Groups)
            .Include(a => a.TeamMembers)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(a => a.Name.Contains(searchTerm) || a.Description.Contains(searchTerm));
        }

        if (showActiveOnly == true)
        {
            query = query.Where(a => a.IsActive);
        }

        var totalItems = await query.CountAsync();
        var pageSize = 10;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var activities = await query
            .OrderByDescending(a => a.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityViewModel
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                IsActive = a.IsActive,
                PricePerDay = a.PricePerDay,
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                OrganisationId = a.OrganisationId,
                OrganisationName = a.Organisation.Name,
                BookingsCount = a.Bookings.Count,
                GroupsCount = a.Groups.Count,
                TeamMembersCount = a.TeamMembers.Count
            })
            .ToListAsync();

        var viewModel = new ActivityListViewModel
        {
            Activities = activities,
            SearchTerm = searchTerm,
            ShowActiveOnly = showActiveOnly,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = pageSize
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var activity = await _context.Activities
            .Include(a => a.Organisation)
            .Include(a => a.Bookings)
            .Include(a => a.Groups)
            .Include(a => a.TeamMembers)
            .Include(a => a.Days)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound();
        }

        var viewModel = MapToViewModel(activity);
        return View(viewModel);
    }

    public IActionResult Create()
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var viewModel = new ActivityViewModel
        {
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(7),
            IsActive = true,
            OrganisationId = _currentUserService.OrganisationId ?? 0
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        if (viewModel.EndDate < viewModel.StartDate)
        {
            ModelState.AddModelError("EndDate", "La date de fin doit être postérieure à la date de début");
            return View(viewModel);
        }

        var organisationId = _currentUserService.OrganisationId;
        if (!_currentUserService.IsAdmin && organisationId == null)
        {
            return Forbid();
        }

        var activity = new Activity
        {
            Name = viewModel.Name,
            Description = viewModel.Description,
            IsActive = viewModel.IsActive,
            PricePerDay = viewModel.PricePerDay,
            StartDate = viewModel.StartDate,
            EndDate = viewModel.EndDate,
            OrganisationId = _currentUserService.IsAdmin ? viewModel.OrganisationId : organisationId!.Value
        };

        // Generate activity days
        for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
        {
            activity.Days.Add(new ActivityDay
            {
                Label = date.ToString("dddd d MMMM", new System.Globalization.CultureInfo("fr-BE")),
                DayDate = date,
                Week = GetWeekNumber(date, activity.StartDate),
                IsActive = true
            });
        }

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Activity {Name} created by user {UserId}", activity.Name, _currentUserService.UserId);

        TempData["Success"] = "L'activité a été créée avec succès.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var activity = await _context.Activities.FindAsync(id);

        if (activity == null)
        {
            return NotFound();
        }

        var viewModel = new ActivityViewModel
        {
            Id = activity.Id,
            Name = activity.Name,
            Description = activity.Description,
            IsActive = activity.IsActive,
            PricePerDay = activity.PricePerDay,
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            OrganisationId = activity.OrganisationId
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityViewModel viewModel)
    {
        if (id != viewModel.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        if (viewModel.EndDate < viewModel.StartDate)
        {
            ModelState.AddModelError("EndDate", "La date de fin doit être postérieure à la date de début");
            return View(viewModel);
        }

        var activity = await _context.Activities.FindAsync(id);
        if (activity == null)
        {
            return NotFound();
        }

        activity.Name = viewModel.Name;
        activity.Description = viewModel.Description;
        activity.IsActive = viewModel.IsActive;
        activity.PricePerDay = viewModel.PricePerDay;
        activity.StartDate = viewModel.StartDate;
        activity.EndDate = viewModel.EndDate;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Activity {Name} updated by user {UserId}", activity.Name, _currentUserService.UserId);
            TempData["Success"] = "L'activité a été mise à jour avec succès.";
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ActivityExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var activity = await _context.Activities
            .Include(a => a.Organisation)
            .Include(a => a.Bookings)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound();
        }

        var viewModel = MapToViewModel(activity);
        return View(viewModel);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var activity = await _context.Activities
            .Include(a => a.Bookings)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound();
        }

        if (activity.Bookings.Any())
        {
            TempData["Error"] = "Impossible de supprimer cette activité car elle contient des inscriptions.";
            return RedirectToAction(nameof(Index));
        }

        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Activity {Name} deleted by user {UserId}", activity.Name, _currentUserService.UserId);
        TempData["Success"] = "L'activité a été supprimée avec succès.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ActivityExists(int id)
    {
        return await _context.Activities.AnyAsync(a => a.Id == id);
    }

    private static ActivityViewModel MapToViewModel(Activity activity)
    {
        return new ActivityViewModel
        {
            Id = activity.Id,
            Name = activity.Name,
            Description = activity.Description,
            IsActive = activity.IsActive,
            PricePerDay = activity.PricePerDay,
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            OrganisationId = activity.OrganisationId,
            OrganisationName = activity.Organisation?.Name,
            BookingsCount = activity.Bookings?.Count ?? 0,
            GroupsCount = activity.Groups?.Count ?? 0,
            TeamMembersCount = activity.TeamMembers?.Count ?? 0
        };
    }

    private static int GetWeekNumber(DateTime date, DateTime startDate)
    {
        return ((date - startDate).Days / 7) + 1;
    }
}
