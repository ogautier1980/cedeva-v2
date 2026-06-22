using Cedeva.Core.Entities;
using Cedeva.Core.Helpers;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Activities.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Activities;

[Authorize]
public class ActivitiesController : Controller
{
    private const string SortOrderDescending = "desc";
    private const string SortOrderAscending = "asc";
    private const string SessionKeyActivitiesSearchString = "Activities_SearchString";
    private const string SessionKeyActivitiesShowActiveOnly = "Activities_ShowActiveOnly";
    private const string SessionKeyActivitiesSortBy = "Activities_SortBy";
    private const string SessionKeyActivitiesSortOrder = "Activities_SortOrder";
    private const string SessionKeyActivitiesPageNumber = "Activities_PageNumber";

    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ActivitiesController> _logger;
    private readonly IExportFacadeService _exportServices;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IUserDisplayService _userDisplayService;
    private readonly ISessionStateService _sessionState;
    private readonly IEmailTemplateService _templateService;
    private readonly IActivityDayService _activityDayService;

    public ActivitiesController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ActivitiesController> logger,
        IExportFacadeService exportServices,
        IStringLocalizer<SharedResources> localizer,
        IUserDisplayService userDisplayService,
        ISessionStateService sessionState,
        IEmailTemplateService templateService,
        IActivityDayService activityDayService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _exportServices = exportServices;
        _localizer = localizer;
        _userDisplayService = userDisplayService;
        _sessionState = sessionState;
        _templateService = templateService;
        _activityDayService = activityDayService;
    }

    public async Task<IActionResult> Index([FromQuery] ActivityQueryParameters queryParams)
    {
        // Check if any query parameters were provided in the actual HTTP request
        bool hasQueryParams = Request.Query.Count > 0;

        // If query params provided, store them and redirect to clean URL
        if (hasQueryParams)
        {
            StoreActivityFiltersToSession(queryParams);
            TempData[ControllerExtensions.KeepFiltersKey] = true;
            return RedirectToAction(nameof(Index));
        }

        // If not keeping filters (no redirect, just navigation/F5), clear them
        if (TempData[ControllerExtensions.KeepFiltersKey] == null)
        {
            ClearActivityFilters();
        }

        // Load filters from state (will be empty if just cleared)
        LoadActivityFiltersFromSession(queryParams);

        var query = _context.Activities
            .IncludeAll()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryParams.SearchString))
        {
            query = query.Where(a => a.Name.Contains(queryParams.SearchString) || a.Description.Contains(queryParams.SearchString));
        }

        if (queryParams.ShowActiveOnly == true)
        {
            query = query.Where(a => a.IsActive);
        }

        // Apply sorting
        query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortOrder?.ToLowerInvariant()) switch
        {
            ("name", SortOrderAscending) => query.OrderBy(a => a.Name),
            ("name", SortOrderDescending) => query.OrderByDescending(a => a.Name),
            ("startdate", SortOrderAscending) => query.OrderBy(a => a.StartDate),
            ("startdate", SortOrderDescending) => query.OrderByDescending(a => a.StartDate),
            ("enddate", SortOrderAscending) => query.OrderBy(a => a.EndDate),
            ("enddate", SortOrderDescending) => query.OrderByDescending(a => a.EndDate),
            ("isactive", SortOrderAscending) => query.OrderBy(a => a.IsActive),
            ("isactive", SortOrderDescending) => query.OrderByDescending(a => a.IsActive),
            _ => query.OrderByDescending(a => a.StartDate)
        };

        var pagedResult = await query
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
            .ToPaginatedListAsync(queryParams.PageNumber, queryParams.PageSize);

        ViewData["SearchString"] = queryParams.SearchString;
        ViewData["ShowActiveOnly"] = queryParams.ShowActiveOnly;
        ViewData["SortBy"] = queryParams.SortBy;
        ViewData["SortOrder"] = queryParams.SortOrder;
        ViewData["PageNumber"] = pagedResult.PageNumber;
        ViewData["PageSize"] = pagedResult.PageSize;
        ViewData["TotalPages"] = pagedResult.TotalPages;
        ViewData["TotalItems"] = pagedResult.TotalItems;

        var viewModel = new ActivityListViewModel
        {
            Activities = pagedResult.Items,
            SearchTerm = queryParams.SearchString,
            ShowActiveOnly = queryParams.ShowActiveOnly,
            CurrentPage = pagedResult.PageNumber,
            TotalPages = pagedResult.TotalPages,
            PageSize = pagedResult.PageSize
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Details(int id)
    {
        var activity = await _context.Activities
            .IncludeAllWithDays()
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
            await ReloadOrganisationsForAdmin();
            return View(viewModel);
        }

        if (viewModel.EndDate < viewModel.StartDate)
        {
            ModelState.AddModelError("EndDate", _localizer["Validation.EndDateAfterStartDate"]);
            await ReloadOrganisationsForAdmin();
            return View(viewModel);
        }

        var organisationId = _currentUserService.OrganisationId;
        if (!_currentUserService.IsAdmin && organisationId == null)
        {
            return Forbid();
        }

        if (_currentUserService.IsAdmin && viewModel.OrganisationId == 0)
        {
            ModelState.AddModelError("OrganisationId", _localizer["Validation.OrganisationRequired"]);
            await ReloadOrganisationsForAdmin();
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

        GenerateActivityDays(activity);

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Seed the new activity with a copy of the organisation's template library.
        await _templateService.CopyOrganisationTemplatesToActivityAsync(activity.OrganisationId, activity.Id);

        await CreateActivityGroupsAsync(activity.Id, viewModel.NewGroups);
        await CreateActivityQuestionsAsync(activity.Id, viewModel.NewQuestions);

        _logger.LogInformation("Activity {Name} created by user {UserId} with {GroupCount} groups and {QuestionCount} questions",
            activity.Name, _currentUserService.UserId, viewModel.NewGroups?.Count ?? 0, viewModel.NewQuestions?.Count ?? 0);

        return this.RedirectToIndexWithSuccess(_localizer["Message.ActivityCreated"].Value);
    }

    private async Task ReloadOrganisationsForAdmin()
    {
        if (_currentUserService.IsAdmin)
        {
            ViewBag.Organisations = await _context.Organisations
                .IgnoreQueryFilters()
                .Select(o => new { o.Id, o.Name })
                .ToListAsync();
        }
    }

    private async Task CreateActivityGroupsAsync(int activityId, IEnumerable<NewActivityGroupViewModel>? groups)
    {
        if (groups == null || !groups.Any())
            return;

        foreach (var groupVm in groups.Where(g => !string.IsNullOrWhiteSpace(g.Label)))
        {
            var group = new ActivityGroup
            {
                Label = groupVm.Label!.Trim(),
                Capacity = groupVm.Capacity,
                ActivityId = activityId
            };
            _context.ActivityGroups.Add(group);
        }
        await _context.SaveChangesAsync();
    }

    private async Task CreateActivityQuestionsAsync(int activityId, IEnumerable<NewActivityQuestionViewModel>? questions)
    {
        if (questions == null || !questions.Any())
            return;

        var displayOrder = 1;
        foreach (var questionVm in questions.Where(q => !string.IsNullOrWhiteSpace(q.QuestionText)))
        {
            var question = new ActivityQuestion
            {
                ActivityId = activityId,
                QuestionText = questionVm.QuestionText!.Trim(),
                QuestionType = questionVm.QuestionType,
                IsRequired = questionVm.IsRequired,
                Options = questionVm.Options?.Trim(),
                DisplayOrder = displayOrder++,
                IsActive = true
            };
            _context.ActivityQuestions.Add(question);
        }
        await _context.SaveChangesAsync();
    }

    private static void GenerateActivityDays(Activity activity) => ActivityDayGenerator.GenerateDays(activity);

    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var activity = await _context.Activities
            .IncludeAllWithDays()
            .Include(a => a.AdditionalQuestions)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
        {
            return NotFound();
        }

        // Ensure all days between StartDate and EndDate exist in DB (create missing days as inactive)
        ActivityDayGenerator.EnsureAllDaysExist(activity);
        await _context.SaveChangesAsync();

        ViewData["ReturnUrl"] = returnUrl;
        var viewModel = MapToViewModel(activity);

        // Load existing questions
        if (activity.AdditionalQuestions != null && activity.AdditionalQuestions.Any())
        {
            viewModel.ExistingQuestions = activity.AdditionalQuestions
                .OrderBy(q => q.DisplayOrder)
                .Select(q => new ExistingActivityQuestionViewModel
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType,
                    IsRequired = q.IsRequired,
                    Options = q.Options,
                    DisplayOrder = q.DisplayOrder,
                    IsActive = q.IsActive
                }).ToList();
        }

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
                .IncludeAllWithDays()
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
            ActivityDayGenerator.HandleDateRangeChanges(activity, viewModel.StartDate, viewModel.EndDate, oldStartDate, oldEndDate);
        }

        // Handle day activation/deactivation changes (booking-aware; may need confirmation/info)
        if (ActiveDayIds != null)
        {
            viewModel = MapToViewModel(activity);
            var dayResult = await _activityDayService.ApplyDayActivationChangesAsync(
                activity, ActiveDayIds, addDaysToBookings == "true", removeDaysConfirmed == "true");

            if (dayResult.Outcome == DayActivationOutcome.NeedsRemoveConfirmation)
            {
                TempData[ControllerExtensions.WarningMessageKey] = string.Format(
                    _localizer["Activities.DeactivateDaysWarning"].Value,
                    dayResult.AffectedBookings, string.Join(", ", dayResult.DayLabels!));
                TempData["ActiveDaysAfterDeactivation"] = string.Join(",", dayResult.RemainingActiveDayIds!);
                return View(viewModel);
            }
            if (dayResult.Outcome == DayActivationOutcome.NeedsActivateInfo)
            {
                TempData[ControllerExtensions.InfoMessageKey] = string.Format(
                    _localizer["Activities.ActivateDaysInfo"].Value, string.Join(", ", dayResult.DayLabels!));
                TempData["ActivatedDays"] = string.Join(",", dayResult.ActivatedDayIds!);
                return View(viewModel);
            }
        }

        await UpdateExistingQuestionsAsync(viewModel, id);
        await AddNewQuestionsAsync(viewModel, id);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Activity {Name} updated by user {UserId}", activity.Name, _currentUserService.UserId);
            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ActivityUpdated"].Value;
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

        return this.RedirectToReturnUrlOrAction(returnUrl, nameof(Index));
    }

    /// <summary>
    /// AJAX day-range editor used on the Edit page: extend the range by one day before/after, or
    /// shrink it by deactivating the edge day. Shrinking a day that still has reserved bookings
    /// requires <paramref name="confirmed"/>=true; on confirmation those BookingDays are removed and
    /// each affected booking's total is decremented by one PricePerDay (excursion costs preserved).
    /// Returns the new range + day list as JSON so the page can update live.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustActivityDays(int id, string edge, string op, bool confirmed = false)
    {
        var result = await _activityDayService.AdjustAsync(id, edge, op, confirmed);

        return result.Outcome switch
        {
            AdjustDaysOutcome.NotFound => NotFound(),
            AdjustDaysOutcome.BadRequest => BadRequest(),
            AdjustDaysOutcome.CannotRemoveLastDay =>
                Json(new { success = false, message = _localizer["Activities.CannotRemoveLastDay"].Value }),
            AdjustDaysOutcome.NeedsConfirmation =>
                Json(new { needsConfirmation = true, reservedCount = result.ReservedCount, label = result.Label }),
            _ => Json(new
            {
                success = true,
                startDate = result.StartDate,
                endDate = result.EndDate,
                activeDaysCount = result.ActiveDaysCount,
                days = result.Days!.Select(d => new { dayId = d.DayId, label = d.Label, date = d.Date, isActive = d.IsActive, week = d.Week })
            })
        };
    }

    private async Task UpdateExistingQuestionsAsync(ActivityViewModel viewModel, int activityId)
    {
        if (viewModel.ExistingQuestions == null || !viewModel.ExistingQuestions.Any()) return;

        var existingQuestions = await _context.ActivityQuestions
            .Where(q => q.ActivityId == activityId)
            .ToListAsync();

        foreach (var questionVm in viewModel.ExistingQuestions)
        {
            var question = existingQuestions.FirstOrDefault(q => q.Id == questionVm.Id);
            if (question == null) continue;
            question.QuestionText = questionVm.QuestionText.Trim();
            question.QuestionType = questionVm.QuestionType;
            question.IsRequired = questionVm.IsRequired;
            question.Options = questionVm.Options?.Trim();
            question.DisplayOrder = questionVm.DisplayOrder;
            question.IsActive = questionVm.IsActive;
            _context.ActivityQuestions.Update(question);
        }
    }

    private async Task AddNewQuestionsAsync(ActivityViewModel viewModel, int activityId)
    {
        if (viewModel.NewQuestions == null || !viewModel.NewQuestions.Any()) return;

        var maxDisplayOrder = await _context.ActivityQuestions
            .Where(q => q.ActivityId == activityId)
            .MaxAsync(q => (int?)q.DisplayOrder) ?? 0;

        foreach (var questionVm in viewModel.NewQuestions.Where(q => !string.IsNullOrWhiteSpace(q.QuestionText)))
        {
            maxDisplayOrder++;
            _context.ActivityQuestions.Add(new ActivityQuestion
            {
                ActivityId = activityId,
                QuestionText = questionVm.QuestionText.Trim(),
                QuestionType = questionVm.QuestionType,
                IsRequired = questionVm.IsRequired,
                Options = questionVm.Options?.Trim(),
                DisplayOrder = maxDisplayOrder,
                IsActive = true
            });
        }
    }

    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _context.Activities
            .IncludeBasic()
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
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Message.ActivityHasBookings"].Value;
            return RedirectToAction(nameof(Index));
        }

        // The Activity -> EmailTemplate FK is NO ACTION (to avoid multiple cascade paths on SQL
        // Server), so remove the activity's templates explicitly before deleting it.
        var activityTemplates = await _context.EmailTemplates.Where(t => t.ActivityId == id).ToListAsync();
        if (activityTemplates.Count > 0)
            _context.EmailTemplates.RemoveRange(activityTemplates);

        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Activity {Name} deleted by user {UserId}", activity.Name, _currentUserService.UserId);
        return this.RedirectToIndexWithSuccess(_localizer["Message.ActivityDeleted"].Value);
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
        viewModel.CreatedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(createdBy);

        // Fetch modified by user info (if exists)
        if (!string.IsNullOrEmpty(modifiedBy))
        {
            viewModel.ModifiedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(modifiedBy);
        }
    }


    // GET: Activities/Export
    public async Task<IActionResult> Export(string? searchTerm, bool? showActiveOnly)
    {
        var query = _context.Activities
            .IncludeAll()
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
        var excelData = _exportServices.Excel.ExportToExcel(activities, sheetName, columns);
        var fileName = $"{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Activities/ExportPdf
    public async Task<IActionResult> ExportPdf(string? searchTerm, bool? showActiveOnly)
    {
        var query = _context.Activities
            .IncludeAll()
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
        var pdfData = _exportServices.Pdf.ExportToPdf(activities, title, columns);
        var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(pdfData, "application/pdf", fileName);
    }

    private void StoreActivityFiltersToSession(ActivityQueryParameters queryParams)
    {
        if (!string.IsNullOrWhiteSpace(queryParams.SearchString))
            _sessionState.Set(SessionKeyActivitiesSearchString, queryParams.SearchString, persistToCookie: false);

        if (queryParams.ShowActiveOnly.HasValue)
            _sessionState.Set(SessionKeyActivitiesShowActiveOnly, queryParams.ShowActiveOnly, persistToCookie: false);

        if (!string.IsNullOrWhiteSpace(queryParams.SortBy))
            _sessionState.Set(SessionKeyActivitiesSortBy, queryParams.SortBy, persistToCookie: false);

        if (!string.IsNullOrWhiteSpace(queryParams.SortOrder))
            _sessionState.Set(SessionKeyActivitiesSortOrder, queryParams.SortOrder, persistToCookie: false);

        if (queryParams.PageNumber > 1)
            _sessionState.Set(SessionKeyActivitiesPageNumber, queryParams.PageNumber.ToString(), persistToCookie: false);
    }

    private void ClearActivityFilters()
    {
        _sessionState.Clear(SessionKeyActivitiesSearchString);
        _sessionState.Clear(SessionKeyActivitiesShowActiveOnly);
        _sessionState.Clear(SessionKeyActivitiesSortBy);
        _sessionState.Clear(SessionKeyActivitiesSortOrder);
        _sessionState.Clear(SessionKeyActivitiesPageNumber);
    }

    private void LoadActivityFiltersFromSession(ActivityQueryParameters queryParams)
    {
        queryParams.SearchString = _sessionState.Get(SessionKeyActivitiesSearchString);
        queryParams.ShowActiveOnly = _sessionState.Get<bool>(SessionKeyActivitiesShowActiveOnly);
        queryParams.SortBy = _sessionState.Get(SessionKeyActivitiesSortBy);
        queryParams.SortOrder = _sessionState.Get(SessionKeyActivitiesSortOrder);

        var pageNumberStr = _sessionState.Get(SessionKeyActivitiesPageNumber);
        if (!string.IsNullOrEmpty(pageNumberStr) && int.TryParse(pageNumberStr, out var pageNum))
        {
            queryParams.PageNumber = pageNum;
        }
    }
}
