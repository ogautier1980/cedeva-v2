using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.ActivityManagement.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.ActivityManagement;

[Authorize]
public class ExcursionsController : Controller
{
    private const string ErrorGeneric = "Error";
    private const string ErrorRegistrationNotFound = "Error.RegistrationNotFound";

    private readonly CedevaDbContext _context;
    private readonly IExcursionService _excursionService;
    private readonly IExcursionViewModelBuilderService _viewModelBuilder;
    private readonly ISessionStateService _sessionState;
    private readonly ILogger<ExcursionsController> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IUserDisplayService _userDisplayService;

    public ExcursionsController(
        CedevaDbContext context,
        IExcursionService excursionService,
        IExcursionViewModelBuilderService viewModelBuilder,
        ISessionStateService sessionState,
        ILogger<ExcursionsController> logger,
        IStringLocalizer<SharedResources> localizer,
        IUserDisplayService userDisplayService)
    {
        _context = context;
        _excursionService = excursionService;
        _viewModelBuilder = viewModelBuilder;
        _sessionState = sessionState;
        _logger = logger;
        _localizer = localizer;
        _userDisplayService = userDisplayService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        var activityId = id ?? _sessionState.Get<int>("ActivityId");
        if (activityId == null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity == null)
            return NotFound();

        _sessionState.Set<int>("ActivityId", activityId.Value);

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

        this.SetActivityViewData(activity.Id, activity.Name);

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? id)
    {
        var activityId = id ?? _sessionState.Get<int>("ActivityId");
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

        this.SetActivityViewData(activity.Id, activity.Name);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateExcursionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await ReloadActivityForViewModel(model);
            return View(model);
        }

        if (model.SelectedGroupIds == null || model.SelectedGroupIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedGroupIds), _localizer["Validation.AtLeastOneGroupRequired"]);
            await ReloadActivityForViewModel(model);
            return View(model);
        }

        // Validate that the excursion date is on an active day of the activity
        var activityDayExists = await _context.ActivityDays
            .AnyAsync(d => d.ActivityId == model.ActivityId
                        && d.DayDate.Date == model.ExcursionDate.Date
                        && d.IsActive);

        if (!activityDayExists)
        {
            ModelState.AddModelError(nameof(model.ExcursionDate), _localizer["Validation.ExcursionDateMustBeActiveDay"]);
            await ReloadActivityForViewModel(model);
            return View(model);
        }

        var (startTime, endTime) = ParseTimeFields(model.StartTime, model.EndTime);

        var excursion = new Excursion
        {
            Name = model.Name,
            Description = model.Description,
            ExcursionDate = model.ExcursionDate,
            StartTime = startTime,
            EndTime = endTime,
            Cost = model.Cost,
            Type = model.Type,
            ActivityId = model.ActivityId,
            IsActive = true
        };

        _context.Excursions.Add(excursion);
        await _context.SaveChangesAsync();

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

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ExcursionCreated"].ToString();
        return RedirectToAction(nameof(Index), new { id = model.ActivityId });
    }

    private async Task ReloadActivityForViewModel(CreateExcursionViewModel model)
    {
        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == model.ActivityId);

        if (activity != null)
        {
            model.Activity = activity;
            model.AvailableGroups = activity.Groups.ToList();
            this.SetActivityViewData(activity.Id, activity.Name);
        }
    }

    private async Task ReloadActivityForViewModel(EditExcursionViewModel model)
    {
        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == model.ActivityId);

        if (activity != null)
        {
            model.Activity = activity;
            model.AvailableGroups = activity.Groups.ToList();
            this.SetActivityViewData(activity.Id, activity.Name);
        }
    }

    private (TimeSpan? startTime, TimeSpan? endTime) ParseTimeFields(string? startTimeStr, string? endTimeStr)
    {
        TimeSpan? startTime = null;
        TimeSpan? endTime = null;

        if (!string.IsNullOrWhiteSpace(startTimeStr) && TimeSpan.TryParse(startTimeStr, out var parsedStartTime))
        {
            startTime = parsedStartTime;
        }

        if (!string.IsNullOrWhiteSpace(endTimeStr) && TimeSpan.TryParse(endTimeStr, out var parsedEndTime))
        {
            endTime = parsedEndTime;
        }

        return (startTime, endTime);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .Include(e => e.Registrations)
                .ThenInclude(r => r.Booking)
                    .ThenInclude(b => b.Child)
            .Include(e => e.Expenses)
            .Include(e => e.TeamMembers)
                .ThenInclude(tm => tm.TeamMember)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);

        // Fetch user display names for audit fields
        ViewBag.CreatedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(excursion.CreatedBy);
        if (!string.IsNullOrEmpty(excursion.ModifiedBy))
        {
            ViewBag.ModifiedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(excursion.ModifiedBy);
        }

        return View(excursion);
    }


    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
                .ThenInclude(a => a.Groups)
            .Include(e => e.ExcursionGroups)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        var viewModel = new EditExcursionViewModel
        {
            Id = excursion.Id,
            ActivityId = excursion.ActivityId,
            Activity = excursion.Activity,
            Name = excursion.Name,
            Description = excursion.Description,
            ExcursionDate = excursion.ExcursionDate,
            StartTime = excursion.StartTime?.ToString(@"hh\:mm"),
            EndTime = excursion.EndTime?.ToString(@"hh\:mm"),
            Cost = excursion.Cost,
            Type = excursion.Type,
            SelectedGroupIds = excursion.ExcursionGroups.Select(eg => eg.ActivityGroupId).ToList(),
            AvailableGroups = excursion.Activity.Groups.ToList()
        };

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditExcursionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await ReloadActivityForViewModel(model);
            return View(model);
        }

        if (model.SelectedGroupIds == null || model.SelectedGroupIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedGroupIds), _localizer["Validation.AtLeastOneGroupRequired"]);
            await ReloadActivityForViewModel(model);
            return View(model);
        }

        // Validate that the excursion date is on an active day of the activity
        var activityDayExists = await _context.ActivityDays
            .AnyAsync(d => d.ActivityId == model.ActivityId
                        && d.DayDate.Date == model.ExcursionDate.Date
                        && d.IsActive);

        if (!activityDayExists)
        {
            ModelState.AddModelError(nameof(model.ExcursionDate), _localizer["Validation.ExcursionDateMustBeActiveDay"]);
            await ReloadActivityForViewModel(model);
            return View(model);
        }

        var excursion = await _context.Excursions
            .Include(e => e.ExcursionGroups)
            .FirstOrDefaultAsync(e => e.Id == model.Id);

        if (excursion == null)
            return NotFound();

        var (startTime, endTime) = ParseTimeFields(model.StartTime, model.EndTime);

        excursion.Name = model.Name;
        excursion.Description = model.Description;
        excursion.ExcursionDate = model.ExcursionDate;
        excursion.StartTime = startTime;
        excursion.EndTime = endTime;
        excursion.Cost = model.Cost;
        excursion.Type = model.Type;

        _context.ExcursionGroups.RemoveRange(excursion.ExcursionGroups);

        foreach (var groupId in model.SelectedGroupIds)
        {
            _context.ExcursionGroups.Add(new ExcursionGroup
            {
                ExcursionId = excursion.Id,
                ActivityGroupId = groupId
            });
        }

        await _context.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ExcursionUpdated"].ToString();
        return RedirectToAction(nameof(Index), new { id = model.ActivityId });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);

        return View(excursion);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        if (excursion.Registrations.Count > 0)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Excursion.CannotDeleteRegistrations"].ToString();
            return RedirectToAction(nameof(Index), new { id = excursion.ActivityId });
        }

        // Soft delete
        excursion.IsActive = false;
        await _context.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ExcursionDeleted"].ToString();
        return RedirectToAction(nameof(Index), new { id = excursion.ActivityId });
    }

    [HttpGet]
    public async Task<IActionResult> Registrations(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        // Use service to build the grouped and sorted children data
        var childrenByGroup = await _viewModelBuilder.BuildRegistrationsByGroupAsync(
            id,
            status => _localizer[$"PaymentStatus.{status}"].ToString());

        var viewModel = new ExcursionRegistrationsViewModel
        {
            Excursion = excursion,
            Activity = excursion.Activity,
            ChildrenByGroup = childrenByGroup
        };

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);
        ViewData["NavSection"] = "Excursions";
        ViewData["NavAction"] = "Registrations";

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterChild(int excursionId, int bookingId)
    {
        try
        {
            await _excursionService.RegisterChildAsync(excursionId, bookingId);
            return Json(new { success = true, message = _localizer["Message.ChildRegistered"].ToString() });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while registering child for excursion {ExcursionId}, booking {BookingId}", excursionId, bookingId);
            return Json(new { success = false, message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while registering child for excursion {ExcursionId}, booking {BookingId}", excursionId, bookingId);
            return Json(new { success = false, message = _localizer[ErrorGeneric].ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while registering child for excursion {ExcursionId}, booking {BookingId}", excursionId, bookingId);
            return Json(new { success = false, message = _localizer[ErrorGeneric].ToString() });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnregisterChild(int excursionId, int bookingId)
    {
        try
        {
            var result = await _excursionService.UnregisterChildAsync(excursionId, bookingId);
            if (result)
            {
                return Json(new { success = true, message = _localizer["Message.ChildUnregistered"].ToString() });
            }
            else
            {
                return Json(new { success = false, message = _localizer[ErrorRegistrationNotFound].ToString() });
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while unregistering child from excursion {ExcursionId}, booking {BookingId}", excursionId, bookingId);
            return Json(new { success = false, message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while unregistering child from excursion {ExcursionId}, booking {BookingId}", excursionId, bookingId);
            return Json(new { success = false, message = _localizer[ErrorGeneric].ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while unregistering child from excursion {ExcursionId}, booking {BookingId}", excursionId, bookingId);
            return Json(new { success = false, message = _localizer[ErrorGeneric].ToString() });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Attendance(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        // Use service to build the grouped and sorted children data
        var childrenByGroup = await _viewModelBuilder.BuildAttendanceByGroupAsync(id);

        var viewModel = new ExcursionAttendanceViewModel
        {
            Excursion = excursion,
            Activity = excursion.Activity,
            ChildrenByGroup = childrenByGroup
        };

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);
        ViewData["NavSection"] = "Excursions";
        ViewData["NavAction"] = "Attendance";

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAttendance(int registrationId, bool isPresent)
    {
        try
        {
            var result = await _excursionService.UpdateAttendanceAsync(registrationId, isPresent);
            if (result)
            {
                return Json(new { success = true, message = _localizer["Message.AttendanceUpdated"].ToString() });
            }
            else
            {
                return Json(new { success = false, message = _localizer[ErrorRegistrationNotFound].ToString() });
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while updating attendance for registration {RegistrationId}", registrationId);
            return Json(new { success = false, message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while updating attendance for registration {RegistrationId}", registrationId);
            return Json(new { success = false, message = _localizer[ErrorGeneric].ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating attendance for registration {RegistrationId}", registrationId);
            return Json(new { success = false, message = _localizer[ErrorGeneric].ToString() });
        }
    }

    [HttpGet]
    public async Task<IActionResult> SendEmail(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        var recipientOptions = GetExcursionRecipientOptions(excursion.ExcursionGroups.Select(eg => eg.ActivityGroup).ToList());

        var viewModel = new SendExcursionEmailViewModel
        {
            ExcursionId = excursion.Id,
            Excursion = excursion,
            Activity = excursion.Activity,
            RecipientOptions = recipientOptions
        };

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);
        ViewData["NavSection"] = "Excursions";
        ViewData["NavAction"] = "SendEmail";

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(SendExcursionEmailViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await ReloadExcursionForViewModel(model);
            return View(model);
        }

        var recipientGroupId = ExtractGroupIdFromRecipient(model.SelectedRecipient);

        var registrations = await _context.ExcursionRegistrations
            .Include(er => er.Booking)
                .ThenInclude(b => b.Child)
                    .ThenInclude(c => c.Parent)
            .Include(er => er.Booking)
                .ThenInclude(b => b.Group)
            .Where(er => er.ExcursionId == model.ExcursionId)
            .ToListAsync();

        if (recipientGroupId.HasValue)
        {
            registrations = registrations.Where(r => r.Booking.GroupId == recipientGroupId.Value).ToList();
        }

        if (!registrations.Any())
        {
            ModelState.AddModelError(string.Empty, _localizer["Message.NoRecipientsFound"]);
            await ReloadExcursionForViewModel(model);
            return View(model);
        }

        var emailCount = registrations.Select(r => r.Booking.Child.Parent.Email).Distinct().Count();

        TempData[ControllerExtensions.SuccessMessageKey] = string.Format(_localizer["Message.EmailSent"].Value, emailCount);
        return RedirectToAction(nameof(Index), new { id = model.Excursion?.ActivityId ?? model.ExcursionId });
    }

    private async Task ReloadExcursionForViewModel(SendExcursionEmailViewModel model)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .FirstOrDefaultAsync(e => e.Id == model.ExcursionId);

        if (excursion != null)
        {
            model.Excursion = excursion;
            model.Activity = excursion.Activity;
            model.RecipientOptions = GetExcursionRecipientOptions(excursion.ExcursionGroups.Select(eg => eg.ActivityGroup).ToList());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Expenses(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .Include(e => e.Expenses)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        var viewModel = new ExcursionExpensesViewModel
        {
            Excursion = excursion,
            Activity = excursion.Activity,
            Expenses = excursion.Expenses.OrderByDescending(e => e.ExpenseDate).ToList(),
            ExpenseDate = DateTime.Today
        };

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);
        ViewData["NavSection"] = "Excursions";
        ViewData["NavAction"] = "Expenses";

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExpense(ExcursionExpensesViewModel model)
    {
        // Validate only the form fields
        ModelState.Remove(nameof(model.Excursion));
        ModelState.Remove(nameof(model.Activity));
        ModelState.Remove(nameof(model.Expenses));

        if (!ModelState.IsValid)
        {
            var excursion = await _context.Excursions
                .Include(e => e.Activity)
                .Include(e => e.Expenses)
                .Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Id == model.Excursion.Id);

            if (excursion != null)
            {
                model.Excursion = excursion;
                model.Activity = excursion.Activity;
                model.Expenses = excursion.Expenses.OrderByDescending(e => e.ExpenseDate).ToList();
                this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);
            }

            ViewData["NavSection"] = "Excursions";
            ViewData["NavAction"] = "Expenses";

            return View("Expenses", model);
        }

        // Create the expense
        var expense = new Expense
        {
            Label = model.Label,
            Description = model.Description,
            Amount = model.Amount,
            Category = model.Category,
            ExpenseDate = model.ExpenseDate,
            OrganizationPaymentSource = model.OrganizationPaymentSource,
            ActivityId = model.Excursion.ActivityId,
            ExcursionId = model.Excursion.Id,
            TeamMemberId = null // Organization expense, not team member expense
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ExpenseAdded"].ToString();
        return RedirectToAction(nameof(Expenses), new { id = model.Excursion.Id });
    }

    [HttpGet]
    public async Task<IActionResult> TeamManagement(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
                .ThenInclude(a => a.TeamMembers)
            .Include(e => e.TeamMembers)
                .ThenInclude(tm => tm.TeamMember)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        // Build a lookup of assigned members
        var assignedMap = excursion.TeamMembers.ToDictionary(
            tm => tm.TeamMemberId,
            tm => tm);

        // All team members assigned to the parent activity
        var teamMembers = excursion.Activity.TeamMembers
            .OrderBy(tm => tm.LastName)
            .ThenBy(tm => tm.FirstName)
            .Select(tm =>
            {
                var assigned = assignedMap.TryGetValue(tm.TeamMemberId, out var etm);
                return new ExcursionTeamMemberInfo
                {
                    TeamMemberId = tm.TeamMemberId,
                    FirstName = tm.FirstName,
                    LastName = tm.LastName,
                    IsAssigned = assigned,
                    IsPresent = assigned && etm!.IsPresent,
                    ExcursionTeamMemberId = assigned ? etm!.Id : null
                };
            })
            .ToList();

        var viewModel = new ExcursionTeamManagementViewModel
        {
            Excursion = excursion,
            Activity = excursion.Activity,
            TeamMembers = teamMembers
        };

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);
        ViewData["NavSection"] = "Excursions";
        ViewData["NavAction"] = "TeamManagement";

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTeamMember(int excursionId, int teamMemberId)
    {
        var existing = await _context.ExcursionTeamMembers
            .FirstOrDefaultAsync(tm => tm.ExcursionId == excursionId && tm.TeamMemberId == teamMemberId);

        if (existing != null)
            return Json(new { success = false, message = _localizer[ErrorGeneric].ToString() });

        _context.ExcursionTeamMembers.Add(new ExcursionTeamMember
        {
            ExcursionId = excursionId,
            TeamMemberId = teamMemberId,
            IsAssigned = true,
            IsPresent = false
        });

        await _context.SaveChangesAsync();
        return Json(new { success = true, message = _localizer["Message.TeamMemberAssigned"].ToString() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignTeamMember(int excursionId, int teamMemberId)
    {
        var existing = await _context.ExcursionTeamMembers
            .FirstOrDefaultAsync(tm => tm.ExcursionId == excursionId && tm.TeamMemberId == teamMemberId);

        if (existing == null)
            return Json(new { success = false, message = _localizer[ErrorGeneric].ToString() });

        _context.ExcursionTeamMembers.Remove(existing);
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = _localizer["Message.TeamMemberUnassigned"].ToString() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTeamAttendance(int excursionTeamMemberId, bool isPresent)
    {
        var etm = await _context.ExcursionTeamMembers
            .FirstOrDefaultAsync(tm => tm.Id == excursionTeamMemberId);

        if (etm == null)
            return Json(new { success = false, message = _localizer["Error.RegistrationNotFound"].ToString() });

        etm.IsPresent = isPresent;
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = _localizer["Message.AttendanceUpdated"].ToString() });
    }

    private List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GetExcursionRecipientOptions(List<ActivityGroup> groups)
    {
        var options = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
        {
            new() { Value = "all_registered", Text = _localizer["Excursion.AllRegistered"] }
        };

        foreach (var group in groups.OrderBy(g => g.Label))
        {
            options.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = $"group_{group.Id}",
                Text = $"{group.Label} - {_localizer["Excursion.RegisteredOnly"]}"
            });
        }

        return options;
    }

    private static int? ExtractGroupIdFromRecipient(string selectedRecipient)
    {
        if (selectedRecipient.StartsWith("group_") &&
            int.TryParse(selectedRecipient.Substring(6), out var groupId))
        {
            return groupId;
        }

        return null;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult BeginExcursions(int id)
    {
        _sessionState.Set<int>("ActivityId", id);
        return RedirectToAction(nameof(Index), new { id });
    }

}
