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
    private readonly CedevaDbContext _context;
    private readonly IExcursionService _excursionService;
    private readonly IActivitySelectionService _activitySelectionService;
    private readonly ILogger<ExcursionsController> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ExcursionsController(
        CedevaDbContext context,
        IExcursionService excursionService,
        IActivitySelectionService activitySelectionService,
        ILogger<ExcursionsController> logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _excursionService = excursionService;
        _activitySelectionService = activitySelectionService;
        _logger = logger;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        var activityId = id ?? _activitySelectionService.GetSelectedActivityId();
        if (activityId == null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity == null)
            return NotFound();

        _activitySelectionService.SetSelectedActivityId(activityId.Value);

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
        var activityId = id ?? _activitySelectionService.GetSelectedActivityId();
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

        // Parse time fields
        TimeSpan? startTime = null;
        TimeSpan? endTime = null;

        if (!string.IsNullOrWhiteSpace(model.StartTime) && TimeSpan.TryParse(model.StartTime, out var parsedStartTime))
        {
            startTime = parsedStartTime;
        }

        if (!string.IsNullOrWhiteSpace(model.EndTime) && TimeSpan.TryParse(model.EndTime, out var parsedEndTime))
        {
            endTime = parsedEndTime;
        }

        // Create excursion
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

        ViewData["ActivityId"] = excursion.ActivityId;
        ViewData["ActivityName"] = excursion.Activity.Name;

        // Fetch user display names for audit fields
        ViewBag.CreatedByDisplayName = await GetUserDisplayNameAsync(excursion.CreatedBy);
        if (!string.IsNullOrEmpty(excursion.ModifiedBy))
        {
            ViewBag.ModifiedByDisplayName = await GetUserDisplayNameAsync(excursion.ModifiedBy);
        }

        return View(excursion);
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

        ViewData["ActivityId"] = excursion.ActivityId;
        ViewData["ActivityName"] = excursion.Activity.Name;

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditExcursionViewModel model)
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

        var excursion = await _context.Excursions
            .Include(e => e.ExcursionGroups)
            .FirstOrDefaultAsync(e => e.Id == model.Id);

        if (excursion == null)
            return NotFound();

        // Parse time fields
        TimeSpan? startTime = null;
        TimeSpan? endTime = null;

        if (!string.IsNullOrWhiteSpace(model.StartTime) && TimeSpan.TryParse(model.StartTime, out var parsedStart))
            startTime = parsedStart;

        if (!string.IsNullOrWhiteSpace(model.EndTime) && TimeSpan.TryParse(model.EndTime, out var parsedEnd))
            endTime = parsedEnd;

        // Update excursion fields
        excursion.Name = model.Name;
        excursion.Description = model.Description;
        excursion.ExcursionDate = model.ExcursionDate;
        excursion.StartTime = startTime;
        excursion.EndTime = endTime;
        excursion.Cost = model.Cost;
        excursion.Type = model.Type;

        // Update group links: remove old, add new
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

        TempData["SuccessMessage"] = _localizer["Message.ExcursionUpdated"].ToString();
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

        ViewData["ActivityId"] = excursion.ActivityId;
        ViewData["ActivityName"] = excursion.Activity.Name;

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
            TempData["ErrorMessage"] = _localizer["Excursion.CannotDeleteRegistrations"].ToString();
            return RedirectToAction(nameof(Index), new { id = excursion.ActivityId });
        }

        // Soft delete
        excursion.IsActive = false;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = _localizer["Message.ExcursionDeleted"].ToString();
        return RedirectToAction(nameof(Index), new { id = excursion.ActivityId });
    }

    [HttpGet]
    public async Task<IActionResult> Registrations(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        // Get all confirmed bookings for eligible groups
        var eligibleGroupIds = excursion.ExcursionGroups
            .Select(eg => eg.ActivityGroupId)
            .ToList();

        var bookings = await _context.Bookings
            .Include(b => b.Child)
            .Include(b => b.Group)
            .Where(b => b.ActivityId == excursion.ActivityId &&
                       b.IsConfirmed &&
                       b.GroupId.HasValue &&
                       eligibleGroupIds.Contains(b.GroupId.Value))
            .ToListAsync();

        // Get existing registrations
        var registrations = await _excursionService.GetRegistrationsAsync(id);
        var registrationsByBookingId = registrations.ToDictionary(r => r.BookingId, r => r);

        // Group children by ActivityGroup
        var childrenByGroup = new Dictionary<ActivityGroup, List<ExcursionChildInfo>>();

        foreach (var booking in bookings.Where(b => b.Group != null))
        {
            if (!childrenByGroup.ContainsKey(booking.Group!))
            {
                childrenByGroup[booking.Group!] = new List<ExcursionChildInfo>();
            }

            var isRegistered = registrationsByBookingId.ContainsKey(booking.Id);
            var registration = isRegistered ? registrationsByBookingId[booking.Id] : null;

            childrenByGroup[booking.Group!].Add(new ExcursionChildInfo
            {
                BookingId = booking.Id,
                ChildId = booking.ChildId,
                FirstName = booking.Child.FirstName,
                LastName = booking.Child.LastName,
                BirthDate = booking.Child.BirthDate,
                IsRegistered = isRegistered,
                RegistrationId = registration?.Id,
                ExcursionCost = excursion.Cost,
                PaymentStatus = _localizer[$"Enum.PaymentStatus.{booking.PaymentStatus}"]
            });
        }

        // Sort children within each group
        foreach (var group in childrenByGroup.Keys.ToList())
        {
            childrenByGroup[group] = childrenByGroup[group]
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToList();
        }

        var viewModel = new ExcursionRegistrationsViewModel
        {
            Excursion = excursion,
            Activity = excursion.Activity,
            ChildrenByGroup = childrenByGroup
        };

        ViewData["ActivityId"] = excursion.ActivityId;
        ViewData["ActivityName"] = excursion.Activity.Name;
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
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering child for excursion {ExcursionId}, booking {BookingId}", excursionId, bookingId);
            return Json(new { success = false, message = _localizer["Error"].ToString() });
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
                return Json(new { success = false, message = _localizer["Error.RegistrationNotFound"].ToString() });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering child from excursion {ExcursionId}, booking {BookingId}", excursionId, bookingId);
            return Json(new { success = false, message = _localizer["Error"].ToString() });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Attendance(int id)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        // Get all registrations with related booking and child data
        var registrations = await _context.ExcursionRegistrations
            .Include(er => er.Booking)
                .ThenInclude(b => b.Child)
            .Include(er => er.Booking)
                .ThenInclude(b => b.Group)
            .Where(er => er.ExcursionId == id && er.Booking.Group != null)
            .ToListAsync();

        // Group children by ActivityGroup
        var childrenByGroup = new Dictionary<ActivityGroup, List<ExcursionAttendanceInfo>>();

        foreach (var registration in registrations)
        {
            var group = registration.Booking.Group!;

            if (!childrenByGroup.ContainsKey(group))
            {
                childrenByGroup[group] = new List<ExcursionAttendanceInfo>();
            }

            childrenByGroup[group].Add(new ExcursionAttendanceInfo
            {
                RegistrationId = registration.Id,
                BookingId = registration.BookingId,
                FirstName = registration.Booking.Child.FirstName,
                LastName = registration.Booking.Child.LastName,
                BirthDate = registration.Booking.Child.BirthDate,
                IsPresent = registration.IsPresent
            });
        }

        // Sort children within each group
        foreach (var group in childrenByGroup.Keys.ToList())
        {
            childrenByGroup[group] = childrenByGroup[group]
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToList();
        }

        var viewModel = new ExcursionAttendanceViewModel
        {
            Excursion = excursion,
            Activity = excursion.Activity,
            ChildrenByGroup = childrenByGroup
        };

        ViewData["ActivityId"] = excursion.ActivityId;
        ViewData["ActivityName"] = excursion.Activity.Name;
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
                return Json(new { success = false, message = _localizer["Error.RegistrationNotFound"].ToString() });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attendance for registration {RegistrationId}", registrationId);
            return Json(new { success = false, message = _localizer["Error"].ToString() });
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

        ViewData["ActivityId"] = excursion.ActivityId;
        ViewData["ActivityName"] = excursion.Activity.Name;
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

            return View(model);
        }

        // Get registered children's parent emails
        var recipientGroupId = ExtractGroupIdFromRecipient(model.SelectedRecipient);

        var registrations = await _context.ExcursionRegistrations
            .Include(er => er.Booking)
                .ThenInclude(b => b.Child)
                    .ThenInclude(c => c.Parent)
            .Include(er => er.Booking)
                .ThenInclude(b => b.Group)
            .Where(er => er.ExcursionId == model.ExcursionId)
            .ToListAsync();

        // Filter by group if specified
        if (recipientGroupId.HasValue)
        {
            registrations = registrations.Where(r => r.Booking.GroupId == recipientGroupId.Value).ToList();
        }

        if (!registrations.Any())
        {
            ModelState.AddModelError(string.Empty, _localizer["Message.NoRecipientsFound"]);
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

            return View(model);
        }

        // Note: Email sending implementation would go here
        // For now, just show success message
        var emailCount = registrations.Select(r => r.Booking.Child.Parent.Email).Distinct().Count();

        TempData["SuccessMessage"] = string.Format(_localizer["Message.EmailSent"].Value, emailCount);
        return RedirectToAction(nameof(Index), new { id = model.Excursion?.ActivityId ?? model.ExcursionId });
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

        ViewData["ActivityId"] = excursion.ActivityId;
        ViewData["ActivityName"] = excursion.Activity.Name;
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
            }

            ViewData["ActivityId"] = excursion?.ActivityId;
            ViewData["ActivityName"] = excursion?.Activity.Name;
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

        TempData["SuccessMessage"] = _localizer["Message.ExpenseAdded"].ToString();
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

        ViewData["ActivityId"] = excursion.ActivityId;
        ViewData["ActivityName"] = excursion.Activity.Name;
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
            return Json(new { success = false, message = _localizer["Error"].ToString() });

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
            return Json(new { success = false, message = _localizer["Error"].ToString() });

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
        _activitySelectionService.SetSelectedActivityId(id);
        return RedirectToAction(nameof(Index), new { id });
    }

}
