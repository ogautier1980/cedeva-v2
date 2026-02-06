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
    private const string TempDataSuccessMessage = "SuccessMessage";
    private const string TempDataErrorMessage = "ErrorMessage";

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

        // Fetch user display names for audit fields
        await PopulateAuditDisplayNamesAsync(viewModel, activity.CreatedBy, activity.ModifiedBy);

        return View(viewModel);
    }

    public async Task<IActionResult> Create()
    {
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
            IncludedPostalCodes = viewModel.IncludedPostalCodes,
            ExcludedPostalCodes = viewModel.ExcludedPostalCodes,
            OrganisationId = _currentUserService.IsAdmin ? viewModel.OrganisationId : organisationId!.Value
        };

        // Generate activity days (weekdays active by default, weekends inactive)
        for (var date = activity.StartDate; date <= activity.EndDate; date = date.AddDays(1))
        {
            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

            activity.Days.Add(new ActivityDay
            {
                Label = date.ToString("dddd d MMMM", new System.Globalization.CultureInfo("fr-BE")),
                DayDate = date,
                Week = GetWeekNumber(date, activity.StartDate),
                IsActive = !isWeekend  // Active by default only for weekdays
            });
        }

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Activity {Name} created by user {UserId}", activity.Name, _currentUserService.UserId);

        TempData[TempDataSuccessMessage] = _localizer["Message.ActivityCreated"].Value;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.Organisation)
            .Include(a => a.Bookings)
            .Include(a => a.Groups)
            .Include(a => a.TeamMembers)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound();
        }

        ViewData["ReturnUrl"] = returnUrl;
        var viewModel = MapToViewModel(activity);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityViewModel viewModel, List<int>? ActiveDayIds, string? addDaysToBookings, string? removeDaysConfirmed, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (id != viewModel.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            var activityForReload = await _context.Activities
                .Include(a => a.Days)
                .Include(a => a.Organisation)
                .Include(a => a.Bookings)
                .Include(a => a.Groups)
                .Include(a => a.TeamMembers)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (activityForReload != null)
            {
                viewModel = MapToViewModel(activityForReload);
            }
            return View(viewModel);
        }

        if (viewModel.EndDate < viewModel.StartDate)
        {
            ModelState.AddModelError("EndDate", _localizer["Validation.EndDateAfterStartDate"]);
            return View(viewModel);
        }

        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.Bookings)
                .ThenInclude(b => b.Days)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound();
        }

        // Update basic properties
        activity.Name = viewModel.Name;
        activity.Description = viewModel.Description;
        activity.IsActive = viewModel.IsActive;
        activity.PricePerDay = viewModel.PricePerDay;

        // Handle date changes and generate/remove days if needed
        var oldStartDate = activity.StartDate;
        var oldEndDate = activity.EndDate;

        activity.StartDate = viewModel.StartDate;
        activity.EndDate = viewModel.EndDate;
        activity.IncludedPostalCodes = viewModel.IncludedPostalCodes;
        activity.ExcludedPostalCodes = viewModel.ExcludedPostalCodes;

        var datesChanged = viewModel.StartDate != oldStartDate || viewModel.EndDate != oldEndDate;
        if (datesChanged)
        {
            HandleDateRangeChanges(activity, viewModel.StartDate, viewModel.EndDate, oldStartDate, oldEndDate);
        }

        // Handle day activation/deactivation changes
        if (ActiveDayIds != null)
        {
            viewModel = MapToViewModel(activity);
            var result = await HandleDayActivationChangesAsync(
                activity, ActiveDayIds, addDaysToBookings, removeDaysConfirmed, viewModel);
            if (result != null)
            {
                return result;
            }
        }

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Activity {Name} updated by user {UserId}", activity.Name, _currentUserService.UserId);
            TempData[TempDataSuccessMessage] = _localizer["Message.ActivityUpdated"].Value;
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

        // Redirect to return URL if provided, otherwise to Index
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
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
            TempData[TempDataErrorMessage] = _localizer["Message.ActivityHasBookings"].Value;
            return RedirectToAction(nameof(Index));
        }

        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Activity {Name} deleted by user {UserId}", activity.Name, _currentUserService.UserId);
        TempData[TempDataSuccessMessage] = _localizer["Message.ActivityDeleted"].Value;

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ActivityExists(int id)
    {
        return await _context.Activities.AnyAsync(a => a.Id == id);
    }

    private static ActivityViewModel MapToViewModel(Activity activity)
    {
        var viewModel = new ActivityViewModel
        {
            Id = activity.Id,
            Name = activity.Name,
            Description = activity.Description,
            IsActive = activity.IsActive,
            PricePerDay = activity.PricePerDay,
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            IncludedPostalCodes = activity.IncludedPostalCodes,
            ExcludedPostalCodes = activity.ExcludedPostalCodes,
            OrganisationId = activity.OrganisationId,
            OrganisationName = activity.Organisation?.Name,
            BookingsCount = activity.Bookings?.Count ?? 0,
            GroupsCount = activity.Groups?.Count ?? 0,
            TeamMembersCount = activity.TeamMembers?.Count ?? 0,

            // Audit fields
            CreatedAt = activity.CreatedAt,
            CreatedBy = activity.CreatedBy,
            ModifiedAt = activity.ModifiedAt,
            ModifiedBy = activity.ModifiedBy
        };

        // Group days by week for Details view
        if (activity.Days != null && activity.Days.Any())
        {
            viewModel.WeeklyDays = activity.Days
                .GroupBy(d => d.Week ?? 0)
                .OrderBy(g => g.Key)
                .Select(g => new WeeklyActivityDaysViewModel
                {
                    WeekNumber = g.Key,
                    WeekLabel = $"Semaine {g.Key}",
                    StartDate = g.Min(d => d.DayDate),
                    EndDate = g.Max(d => d.DayDate),
                    ActiveDaysCount = g.Count(d => d.IsActive),
                    TotalDaysCount = g.Count(),
                    Days = g.OrderBy(d => d.DayDate)
                        .Select(d => new ActivityDayViewModel
                        {
                            DayId = d.DayId,
                            Label = d.Label,
                            DayDate = d.DayDate,
                            DayOfWeek = d.DayDate.DayOfWeek,
                            IsActive = d.IsActive,
                            IsWeekend = d.DayDate.DayOfWeek == DayOfWeek.Saturday || d.DayDate.DayOfWeek == DayOfWeek.Sunday,
                            Week = d.Week
                        })
                        .ToList()
                })
                .ToList();

            // Also populate AllDays for Edit view
            viewModel.AllDays = activity.Days
                .OrderBy(d => d.DayDate)
                .Select(d => new ActivityDayViewModel
                {
                    DayId = d.DayId,
                    Label = d.Label,
                    DayDate = d.DayDate,
                    DayOfWeek = d.DayDate.DayOfWeek,
                    IsActive = d.IsActive,
                    IsWeekend = d.DayDate.DayOfWeek == DayOfWeek.Saturday || d.DayDate.DayOfWeek == DayOfWeek.Sunday,
                    Week = d.Week
                })
                .ToList();
        }

        return viewModel;
    }

    private async Task PopulateAuditDisplayNamesAsync(ActivityViewModel viewModel, string createdBy, string? modifiedBy)
    {
        // Fetch created by user info
        viewModel.CreatedByDisplayName = await GetUserDisplayNameAsync(createdBy);

        // Fetch modified by user info (if exists)
        if (!string.IsNullOrEmpty(modifiedBy))
        {
            viewModel.ModifiedByDisplayName = await GetUserDisplayNameAsync(modifiedBy);
        }
    }

    private async Task<string> GetUserDisplayNameAsync(string userId)
    {
        if (userId == "System")
        {
            return "System";
        }

        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync();

        return user != null
            ? $"{user.FirstName} {user.LastName}".Trim()
            : userId; // Fallback to ID if user not found
    }

    private static int GetWeekNumber(DateTime date, DateTime startDate)
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

    /// <summary>
    /// Handles date range changes by adding missing days, deactivating out-of-range days, and recalculating week numbers.
    /// </summary>
    private static void HandleDateRangeChanges(
        Activity activity,
        DateTime newStartDate,
        DateTime newEndDate,
        DateTime oldStartDate,
        DateTime oldEndDate)
    {
        var existingDates = activity.Days.Select(d => d.DayDate.Date).ToHashSet();

        // Add missing days before old start date
        if (newStartDate < oldStartDate)
        {
            for (var date = newStartDate; date < oldStartDate; date = date.AddDays(1))
            {
                if (!existingDates.Contains(date.Date))
                {
                    var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
                    activity.Days.Add(new ActivityDay
                    {
                        Label = date.ToString("dddd d MMMM", new System.Globalization.CultureInfo("fr-BE")),
                        DayDate = date,
                        Week = GetWeekNumber(date, newStartDate),
                        IsActive = !isWeekend
                    });
                }
            }
        }

        // Add missing days after old end date
        if (newEndDate > oldEndDate)
        {
            for (var date = oldEndDate.AddDays(1); date <= newEndDate; date = date.AddDays(1))
            {
                if (!existingDates.Contains(date.Date))
                {
                    var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
                    activity.Days.Add(new ActivityDay
                    {
                        Label = date.ToString("dddd d MMMM", new System.Globalization.CultureInfo("fr-BE")),
                        DayDate = date,
                        Week = GetWeekNumber(date, newStartDate),
                        IsActive = !isWeekend
                    });
                }
            }
        }

        // Deactivate days that are now outside the new date range
        foreach (var day in activity.Days.Where(d => d.DayDate < newStartDate || d.DayDate > newEndDate))
        {
            day.IsActive = false;
        }

        // Recalculate week numbers for all days
        foreach (var day in activity.Days)
        {
            day.Week = GetWeekNumber(day.DayDate, newStartDate);
        }
    }

    /// <summary>
    /// Handles activation and deactivation of activity days, including booking day updates.
    /// Returns an IActionResult if user confirmation is needed, otherwise null to continue processing.
    /// </summary>
    private async Task<IActionResult?> HandleDayActivationChangesAsync(
        Activity activity,
        List<int> activeDayIds,
        string? addDaysToBookings,
        string? removeDaysConfirmed,
        ActivityViewModel viewModel)
    {
        var currentlyActiveDayIds = activity.Days.Where(d => d.IsActive).Select(d => d.DayId).ToList();

        // Days being activated (were inactive, now active)
        var daysBeingActivated = activeDayIds.Except(currentlyActiveDayIds).ToList();

        // Days being deactivated (were active, now inactive)
        var daysBeingDeactivated = currentlyActiveDayIds.Except(activeDayIds).ToList();

        // Handle deactivation
        var deactivationResult = await HandleDayDeactivationAsync(
            activity, daysBeingDeactivated, removeDaysConfirmed, viewModel);
        if (deactivationResult != null)
        {
            return deactivationResult;
        }

        // Handle activation
        var activationResult = HandleDayActivation(
            activity, daysBeingActivated, addDaysToBookings, viewModel);
        if (activationResult != null)
        {
            return activationResult;
        }

        return null;
    }

    /// <summary>
    /// Handles deactivation of activity days, checking for booking conflicts.
    /// Returns an IActionResult if confirmation is needed, otherwise null.
    /// </summary>
    private async Task<IActionResult?> HandleDayDeactivationAsync(
        Activity activity,
        List<int> daysBeingDeactivated,
        string? removeDaysConfirmed,
        ActivityViewModel viewModel)
    {
        if (!daysBeingDeactivated.Any())
        {
            return null;
        }

        var bookingsWithDeactivatedDays = await _context.BookingDays
            .Where(bd => daysBeingDeactivated.Contains(bd.ActivityDayId) && bd.IsReserved)
            .CountAsync();

        if (bookingsWithDeactivatedDays > 0 && removeDaysConfirmed != "true")
        {
            var deactivatedDaysLabels = activity.Days
                .Where(d => daysBeingDeactivated.Contains(d.DayId))
                .Select(d => d.Label)
                .ToList();

            TempData["Warning"] = string.Format(
                _localizer["Activities.DeactivateDaysWarning"].Value,
                bookingsWithDeactivatedDays,
                string.Join(", ", deactivatedDaysLabels));
            TempData["ActiveDaysAfterDeactivation"] = string.Join(",", activity.Days
                .Where(d => d.IsActive && !daysBeingDeactivated.Contains(d.DayId))
                .Select(d => d.DayId));

            return View(viewModel);
        }

        // User confirmed or no bookings, proceed with deactivation
        foreach (var dayId in daysBeingDeactivated)
        {
            var day = activity.Days.FirstOrDefault(d => d.DayId == dayId);
            if (day != null)
            {
                day.IsActive = false;

                // Remove from all bookings
                var bookingDaysToRemove = await _context.BookingDays
                    .Where(bd => bd.ActivityDayId == dayId)
                    .ToListAsync();

                _context.BookingDays.RemoveRange(bookingDaysToRemove);
            }
        }

        return null;
    }

    /// <summary>
    /// Handles activation of activity days, optionally adding them to existing bookings.
    /// Returns an IActionResult if confirmation is needed, otherwise null.
    /// </summary>
    private IActionResult? HandleDayActivation(
        Activity activity,
        List<int> daysBeingActivated,
        string? addDaysToBookings,
        ActivityViewModel viewModel)
    {
        if (!daysBeingActivated.Any())
        {
            return null;
        }

        // Activate days
        foreach (var dayId in daysBeingActivated)
        {
            var day = activity.Days.FirstOrDefault(d => d.DayId == dayId);
            if (day != null)
            {
                day.IsActive = true;

                // If user confirmed, add to all existing bookings
                if (addDaysToBookings == "true")
                {
                    var bookingsNeedingDay = activity.Bookings
                        .Where(booking => !booking.Days.Any(bd => bd.ActivityDayId == dayId));

                    foreach (var booking in bookingsNeedingDay)
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
        }

        // Show info message if days were activated
        if (addDaysToBookings != "true" && activity.Bookings.Any())
        {
            var activatedDaysLabels = activity.Days
                .Where(d => daysBeingActivated.Contains(d.DayId))
                .Select(d => d.Label)
                .ToList();

            TempData["Info"] = string.Format(
                _localizer["Activities.ActivateDaysInfo"].Value,
                string.Join(", ", activatedDaysLabels));
            TempData["ActivatedDays"] = string.Join(",", daysBeingActivated);

            return View(viewModel);
        }

        return null;
    }

    // GET: Activities/Export
    public async Task<IActionResult> Export(string? searchTerm, bool? showActiveOnly)
    {
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
