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
    private const string SessionKeyActivityId = "ActivityId";
    private const string ErrorGeneric = "Error";
    private const string ErrorRegistrationNotFound = "Error.RegistrationNotFound";

    // Navigation ViewData keys
    private const string NavSectionKey = "NavSection";
    private const string NavActionKey = "NavAction";
    private const string NavSectionExcursions = "Excursions";
    private readonly CedevaDbContext _context;
    private readonly IExcursionService _excursionService;
    private readonly ISessionStateService _sessionState;
    private readonly ILogger<ExcursionsController> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IUserDisplayService _userDisplayService;
    private readonly IEmailFacadeService _emailServices;
    private readonly IQrCodeService _qrCodeService;

    public ExcursionsController(
        CedevaDbContext context,
        IExcursionService excursionService,
        ISessionStateService sessionState,
        ILogger<ExcursionsController> logger,
        IStringLocalizer<SharedResources> localizer,
        IUserDisplayService userDisplayService,
        IEmailFacadeService emailServices,
        IQrCodeService qrCodeService)
    {
        _context = context;
        _excursionService = excursionService;
        _sessionState = sessionState;
        _logger = logger;
        _localizer = localizer;
        _userDisplayService = userDisplayService;
        _emailServices = emailServices;
        _qrCodeService = qrCodeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        var activityId = id ?? _sessionState.Get<int>(SessionKeyActivityId);
        if (activityId == null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity == null)
            return NotFound();

        _sessionState.Set<int>(SessionKeyActivityId, activityId.Value);

        // Load excursions with related data
        var excursions = await _context.Excursions
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .Include(e => e.Registrations)
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
            RegistrationCount = e.Registrations.Count
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
        var activityId = id ?? _sessionState.Get<int>(SessionKeyActivityId);
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

    private static (TimeSpan? startTime, TimeSpan? endTime) ParseTimeFields(string? startTimeStr, string? endTimeStr)
    {
        TimeSpan? startTime = null;
        TimeSpan? endTime = null;

        if (!string.IsNullOrWhiteSpace(startTimeStr) && TimeSpan.TryParse(startTimeStr, System.Globalization.CultureInfo.InvariantCulture, out var parsedStartTime))
        {
            startTime = parsedStartTime;
        }

        if (!string.IsNullOrWhiteSpace(endTimeStr) && TimeSpan.TryParse(endTimeStr, System.Globalization.CultureInfo.InvariantCulture, out var parsedEndTime))
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
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);

        // The children planned in the excursion's group(s) that day — known before any invitation
        // is sent — plus anyone already registered (e.g. booked differently since).
        var plannedBookings = await GetConcernedBookingsAsync(excursion, null, HttpContext.RequestAborted);
        var plannedIds = plannedBookings.Select(b => b.Id).ToHashSet();
        plannedBookings.AddRange(excursion.Registrations.Select(r => r.Booking).Where(b => !plannedIds.Contains(b.Id)));
        ViewBag.PlannedBookings = plannedBookings
            .OrderBy(b => b.Child.LastName).ThenBy(b => b.Child.FirstName)
            .ToList();
        ViewBag.RegisteredBookingIds = excursion.Registrations.Select(r => r.BookingId).ToHashSet();

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
    public async Task<IActionResult> SendEmail(int id, int? templateId)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Activity)
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (excursion == null)
            return NotFound();

        var recipientOptions = GetExcursionRecipientOptions(excursion.ExcursionGroups.Select(eg => eg.ActivityGroup).ToList());

        // How many children the excursion concerns — registrations only exist once the e-mail has
        // been sent, so counting them would show 0 on the screen that triggers the send.
        ViewBag.ConcernedChildrenCount = (await GetConcernedBookingsAsync(excursion, null, HttpContext.RequestAborted)).Count;
        ViewBag.Templates = await _emailServices.Template.GetAllTemplatesAsync(excursion.Activity.OrganisationId, excursion.ActivityId);

        // No explicit template picked (e.g. arriving fresh from "Envoyer un mail") — suggest the
        // organisation's default excursion-proposal template, still fully editable before sending.
        if (templateId == null)
        {
            var defaultTemplate = await _emailServices.Template.GetDefaultTemplateAsync(
                EmailTemplateType.ExcursionProposal, excursion.Activity.OrganisationId, excursion.ActivityId);
            templateId = defaultTemplate?.Id;
        }

        var viewModel = new SendExcursionEmailViewModel
        {
            ExcursionId = excursion.Id,
            Excursion = excursion,
            Activity = excursion.Activity,
            RecipientOptions = recipientOptions,
            PreselectedTemplateId = templateId
        };

        this.SetActivityViewData(excursion.ActivityId, excursion.Activity.Name);
        ViewData[NavSectionKey] = NavSectionExcursions;
        ViewData[NavActionKey] = "SendEmail";

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(SendExcursionEmailViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await ReloadExcursionForViewModel(model);
            return View(model);
        }

        var excursion = await _context.Excursions
            .Include(e => e.ExcursionGroups)
            .FirstOrDefaultAsync(e => e.Id == model.ExcursionId, ct);

        if (excursion == null)
            return NotFound();

        var activityId = excursion.ActivityId;
        var organisationId = await _context.Activities
            .Where(a => a.Id == activityId)
            .Select(a => a.OrganisationId)
            .FirstOrDefaultAsync(ct);
        var organisation = await _context.Organisations.FirstOrDefaultAsync(o => o.Id == organisationId, ct);

        var recipientBookings = await GetConcernedBookingsAsync(
            excursion, ExtractGroupIdFromRecipient(model.SelectedRecipient), ct);

        if (!recipientBookings.Any())
        {
            ModelState.AddModelError(string.Empty, _localizer["Message.NoRecipientsFound"]);
            await ReloadExcursionForViewModel(model);
            return View(model);
        }

        // Announcing the excursion is also what bills it: every child concerned is registered once
        // (idempotent), which adds the excursion cost to their booking — the amount the payment link
        // then collects.
        await _excursionService.EnsureRegisteredAsync(excursion.Id, recipientBookings.Select(b => b.Id).ToList());

        var (attachmentFileName, attachmentFilePath) = await SaveAttachmentAsync(model.AttachmentFile, ct);

        var excursionVariables = new Dictionary<string, string>
        {
            ["excursion_name"] = excursion.Name,
            ["excursion_date"] = excursion.ExcursionDate.ToString("dd/MM/yyyy"),
        };

        // One e-mail per child: the payment link is specific to that child's booking.
        var sentCount = 0;
        foreach (var booking in recipientBookings)
        {
            var checkoutUrl = Url.Action("Checkout", "OnlinePayment", new { bookingId = booking.Id }, Request.Scheme)!;
            var perChildVariables = new Dictionary<string, string>(excursionVariables)
            {
                ["child_firstname"] = booking.Child.FirstName,
                ["child_lastname"] = booking.Child.LastName,
                ["lien_paiement"] = checkoutUrl,
                ["qr_code_paiement"] = _qrCodeService.GenerateImageTag(checkoutUrl),
            };
            var subject = _emailServices.VariableReplacement.ReplaceVariables(model.Subject, booking, organisation!, perChildVariables);
            var message = _emailServices.VariableReplacement.ReplaceVariables(model.Message, booking, organisation!, perChildVariables);

            // Templates that don't spell out %lien_paiement% still get a payment block appended, so
            // the parent always has a way to pay the excursion from the e-mail.
            if (!message.Contains(checkoutUrl, StringComparison.Ordinal))
            {
                message += BuildPaymentBlock(checkoutUrl);
            }

            await _emailServices.Email.SendEmailAsync(booking.Child.Parent.GetEmailAddresses().ToList(), subject, message, attachmentFilePath);
            sentCount++;
        }

        _logger.LogInformation("Sent {SentCount} excursion emails for excursion {ExcursionId}", sentCount, model.ExcursionId);

        TempData[ControllerExtensions.SuccessMessageKey] = string.Format(_localizer["Message.EmailSent"].Value, sentCount);
        return RedirectToAction(nameof(Index), new { id = activityId });
    }

    private static async Task<(string? fileName, string? filePath)> SaveAttachmentAsync(IFormFile? attachmentFile, CancellationToken ct)
    {
        if (attachmentFile == null || attachmentFile.Length == 0)
        {
            return (null, null);
        }

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "attachments");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var fileName = Path.GetFileName(attachmentFile.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        await using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await attachmentFile.CopyToAsync(fileStream, ct);
        }

        return (fileName, filePath);
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
            ViewBag.Templates = await _emailServices.Template.GetAllTemplatesAsync(excursion.Activity.OrganisationId, excursion.ActivityId);
        }
    }

    /// <summary>
    /// The children an excursion concerns: confirmed bookings of the targeted groups that have the
    /// excursion's day reserved — being there that day is what takes part in the excursion. When the
    /// excursion falls outside the activity's days, only the group filter applies.
    /// </summary>
    private async Task<List<Booking>> GetConcernedBookingsAsync(Excursion excursion, int? groupId, CancellationToken ct)
    {
        var targetGroupIds = excursion.ExcursionGroups.Select(eg => eg.ActivityGroupId).ToList();

        var query = _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Group)
            .Where(b => b.ActivityId == excursion.ActivityId && b.IsConfirmed);

        if (targetGroupIds.Count > 0)
        {
            query = query.Where(b => b.GroupId != null && targetGroupIds.Contains(b.GroupId.Value));
        }

        if (groupId.HasValue)
        {
            query = query.Where(b => b.GroupId == groupId.Value);
        }

        var excursionDayId = await _context.ActivityDays
            .Where(d => d.ActivityId == excursion.ActivityId && d.DayDate.Date == excursion.ExcursionDate.Date)
            .Select(d => (int?)d.DayId)
            .FirstOrDefaultAsync(ct);

        if (excursionDayId.HasValue)
        {
            query = query.Where(b => b.Days.Any(bd => bd.ActivityDayId == excursionDayId.Value && bd.IsReserved));
        }

        return await query.ToListAsync(ct);
    }

    private string BuildPaymentBlock(string checkoutUrl) =>
        "<hr>" +
        $"<p style=\"text-align:center;\"><a href=\"{checkoutUrl}\" style=\"background:#007faf;color:#ffffff;" +
        $"padding:12px 24px;border-radius:4px;text-decoration:none;display:inline-block;\">" +
        $"{_localizer["Excursion.PayOnline"].Value}</a></p>" +
        $"<p style=\"text-align:center;\">{_qrCodeService.GenerateImageTag(checkoutUrl)}</p>";

    private List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GetExcursionRecipientOptions(List<ActivityGroup> groups)
    {
        var options = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
        {
            new() { Value = "all", Text = _localizer["Excursion.AllConcernedChildren"] }
        };

        foreach (var group in groups.OrderBy(g => g.Label))
        {
            options.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = $"group_{group.Id}",
                Text = group.Label
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
        _sessionState.Set<int>(SessionKeyActivityId, id);
        return RedirectToAction(nameof(Index), new { id });
    }

}
