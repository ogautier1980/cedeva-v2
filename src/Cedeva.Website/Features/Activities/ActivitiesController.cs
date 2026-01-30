using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Activities.ViewModels;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Activities;

[Authorize]
public class ActivitiesController : Controller
{
    private const string TempDataSuccess = "Success";
    private const string TempDataError = "Error";

    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ActivitiesController> _logger;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ActivitiesController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ActivitiesController> logger,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index(string? searchString, bool? showActiveOnly, string? sortBy = null, string? sortOrder = "asc", int pageNumber = 1, int pageSize = 10)
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

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            query = query.Where(a => a.Name.Contains(searchString) || a.Description.Contains(searchString));
        }

        if (showActiveOnly == true)
        {
            query = query.Where(a => a.IsActive);
        }

        // Apply sorting
        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("name", "asc") => query.OrderBy(a => a.Name),
            ("name", "desc") => query.OrderByDescending(a => a.Name),
            ("startdate", "asc") => query.OrderBy(a => a.StartDate),
            ("startdate", "desc") => query.OrderByDescending(a => a.StartDate),
            ("enddate", "asc") => query.OrderBy(a => a.EndDate),
            ("enddate", "desc") => query.OrderByDescending(a => a.EndDate),
            ("isactive", "asc") => query.OrderBy(a => a.IsActive),
            ("isactive", "desc") => query.OrderByDescending(a => a.IsActive),
            _ => query.OrderByDescending(a => a.StartDate)
        };

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var activities = await query
            .Skip((pageNumber - 1) * pageSize)
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

        ViewData["SearchString"] = searchString;
        ViewData["ShowActiveOnly"] = showActiveOnly;
        ViewData["SortBy"] = sortBy;
        ViewData["SortOrder"] = sortOrder;
        ViewData["PageNumber"] = pageNumber;
        ViewData["PageSize"] = pageSize;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalItems"] = totalItems;

        var viewModel = new ActivityListViewModel
        {
            Activities = activities,
            SearchTerm = searchString,
            ShowActiveOnly = showActiveOnly,
            CurrentPage = pageNumber,
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

    public async Task<IActionResult> Create()
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

        // For admins, load organisations list
        if (_currentUserService.IsAdmin)
        {
            ViewBag.Organisations = await _context.Organisations
                .IgnoreQueryFilters()
                .Select(o => new { o.Id, o.Name })
                .ToListAsync();
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            // Reload organisations for admins
            if (_currentUserService.IsAdmin)
            {
                ViewBag.Organisations = await _context.Organisations
                    .IgnoreQueryFilters()
                    .Select(o => new { o.Id, o.Name })
                    .ToListAsync();
            }
            return View(viewModel);
        }

        if (viewModel.EndDate < viewModel.StartDate)
        {
            ModelState.AddModelError("EndDate", _localizer["Validation.EndDateAfterStartDate"]);
            // Reload organisations for admins
            if (_currentUserService.IsAdmin)
            {
                ViewBag.Organisations = await _context.Organisations
                    .IgnoreQueryFilters()
                    .Select(o => new { o.Id, o.Name })
                    .ToListAsync();
            }
            return View(viewModel);
        }

        var organisationId = _currentUserService.OrganisationId;
        if (!_currentUserService.IsAdmin && organisationId == null)
        {
            return Forbid();
        }

        // For admins, validate that an organisation is selected
        if (_currentUserService.IsAdmin && viewModel.OrganisationId == 0)
        {
            ModelState.AddModelError("OrganisationId", _localizer["Validation.OrganisationRequired"]);
            ViewBag.Organisations = await _context.Organisations
                .IgnoreQueryFilters()
                .Select(o => new { o.Id, o.Name })
                .ToListAsync();
            return View(viewModel);
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

        TempData[TempDataSuccess] = _localizer["Message.ActivityCreated"].Value;
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
            ModelState.AddModelError("EndDate", _localizer["Validation.EndDateAfterStartDate"]);
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
            TempData[TempDataSuccess] = _localizer["Message.ActivityUpdated"].Value;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (!await ActivityExists(id))
            {
                return NotFound();
            }
            _logger.LogError(ex, "Concurrency error updating activity {Id}", id);
            throw new InvalidOperationException($"Failed to update activity {id} due to concurrency conflict", ex);
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
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        var activity = await _context.Activities
            .Include(a => a.Bookings)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound();
        }

        if (activity.Bookings.Any())
        {
            TempData[TempDataError] = _localizer["Message.ActivityHasBookings"].Value;
            return RedirectToAction(nameof(Index));
        }

        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Activity {Name} deleted by user {UserId}", activity.Name, _currentUserService.UserId);
        TempData[TempDataSuccess] = _localizer["Message.ActivityDeleted"].Value;

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

    // GET: Activities/Export
    public async Task<IActionResult> Export(string? searchTerm, bool? showActiveOnly)
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

        var activities = await query
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();

        var columns = new Dictionary<string, Func<Activity, object>>
        {
            { _localizer["Excel.Name"], a => a.Name },
            { _localizer["Excel.Description"], a => a.Description },
            { _localizer["Excel.Organisation"], a => a.Organisation.Name },
            { _localizer["Excel.StartDate"], a => a.StartDate },
            { _localizer["Excel.EndDate"], a => a.EndDate },
            { _localizer["Excel.PricePerDay"], a => (object?)a.PricePerDay ?? "N/A" },
            { _localizer["Excel.Active"], a => a.IsActive },
            { _localizer["Excel.Bookings"], a => a.Bookings.Count },
            { _localizer["Excel.Groups"], a => a.Groups.Count },
            { _localizer["Excel.Team"], a => a.TeamMembers.Count }
        };

        var sheetName = _localizer["Excel.ActivitiesSheet"];
        var excelData = _excelExportService.ExportToExcel(activities, sheetName, columns);
        var fileName = $"{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Activities/ExportPdf
    public async Task<IActionResult> ExportPdf(string? searchTerm, bool? showActiveOnly)
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

        var activities = await query
            .OrderByDescending(a => a.StartDate)
            .ToListAsync();

        var columns = new Dictionary<string, Func<Activity, object>>
        {
            { _localizer["Excel.Name"], a => a.Name },
            { _localizer["Excel.Description"], a => a.Description },
            { _localizer["Excel.Organisation"], a => a.Organisation.Name },
            { _localizer["Excel.StartDate"], a => a.StartDate },
            { _localizer["Excel.EndDate"], a => a.EndDate },
            { _localizer["Excel.PricePerDay"], a => (object?)a.PricePerDay ?? "N/A" },
            { _localizer["Excel.Active"], a => a.IsActive },
            { _localizer["Excel.Bookings"], a => a.Bookings.Count },
            { _localizer["Excel.Groups"], a => a.Groups.Count },
            { _localizer["Excel.Team"], a => a.TeamMembers.Count }
        };

        var title = _localizer["Excel.ActivitiesSheet"];
        var pdfData = _pdfExportService.ExportToPdf(activities, title, columns);
        var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(pdfData, "application/pdf", fileName);
    }
}
