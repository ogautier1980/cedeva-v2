using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.ActivityManagement.ViewModels;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.ActivityManagement;

[Authorize]
public class ActivityManagementController : Controller
{
    private const string RecipientAllParents = "allparents";
    private const string RecipientMedicalSheetReminder = "medicalsheetreminder";
    private const string RecipientUnpaidParents = "unpaidparents";
    private const string RecipientGroupPrefix = "group_";
    private const string RecipientExcursionPrefix = "excursion_";
    private const string TempDataSuccessMessage = "SuccessMessage";
    private const string TempDataErrorMessage = "ErrorMessage";

    private readonly CedevaDbContext _context;
    private readonly ILogger<ActivityManagementController> _logger;
    private readonly IEmailService _emailService;
    private readonly IEmailRecipientService _emailRecipientService;
    private readonly IEmailVariableReplacementService _variableReplacementService;
    private readonly IEmailTemplateService _templateService;
    private readonly ISessionStateService _sessionState;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ActivityManagementController(
        CedevaDbContext context,
        ILogger<ActivityManagementController> logger,
        IEmailService emailService,
        IEmailRecipientService emailRecipientService,
        IEmailVariableReplacementService variableReplacementService,
        IEmailTemplateService templateService,
        ISessionStateService sessionState,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _emailRecipientService = emailRecipientService;
        _variableReplacementService = variableReplacementService;
        _templateService = templateService;
        _sessionState = sessionState;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        // Use service to get or set activity ID
        id ??= _sessionState.Get<int>("ActivityId");

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.Groups)
            .Include(a => a.Bookings)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID for future visits
        _sessionState.Set<int>("ActivityId", id.Value);

        var viewModel = new IndexViewModel
        {
            Activity = activity
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Index")]
    public IActionResult IndexPost(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        _sessionState.Set<int>("ActivityId", id);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> UnconfirmedBookings(int? id)
    {
        id ??= _sessionState.Get<int>("ActivityId");

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .Include(a => a.Bookings)
                .ThenInclude(b => b.Child)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID for future visits
        _sessionState.Set<int>("ActivityId", id.Value);

        var unconfirmedBookings = activity.Bookings
            .Where(b => !b.IsConfirmed)
            .ToList();

        var viewModel = new UnconfirmedBookingsViewModel
        {
            Activity = activity,
            UnconfirmedBookings = unconfirmedBookings,
            GroupOptions = activity.Groups.Select(g => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = g.Id.ToString(),
                Text = g.Label
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("BeginUnconfirmedBookings")]
    public IActionResult UnconfirmedBookingsPost(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        _sessionState.Set<int>("ActivityId", id);
        return RedirectToAction(nameof(UnconfirmedBookings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("BeginGroupAssignment")]
    public IActionResult GroupAssignmentPost(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        _sessionState.Set<int>("ActivityId", id);
        return RedirectToAction(nameof(GroupAssignment));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmBooking(int bookingId, int? groupId)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(UnconfirmedBookings));
        }

        var booking = await _context.Bookings
            .Include(b => b.Activity)
                .ThenInclude(a => a.Groups)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return NotFound();

        // If a group was selected, use it; otherwise assign to "Sans groupe"
        if (groupId.HasValue && groupId.Value > 0)
        {
            booking.GroupId = groupId.Value;
        }
        else
        {
            // Find or create "Sans groupe" group for this activity
            var noGroupLabel = "Sans groupe";
            var noGroup = booking.Activity.Groups.FirstOrDefault(g => g.Label == noGroupLabel);

            if (noGroup == null)
            {
                noGroup = new ActivityGroup
                {
                    ActivityId = booking.Activity.Id,
                    Label = noGroupLabel,
                    Capacity = null
                };
                _context.ActivityGroups.Add(noGroup);
                await _context.SaveChangesAsync(); // Save to get the Id
            }

            booking.GroupId = noGroup.Id;
        }

        booking.IsConfirmed = true;

        await _context.SaveChangesAsync();

        TempData[TempDataSuccessMessage] = _localizer["Message.BookingConfirmed"].Value;
        return RedirectToAction(nameof(UnconfirmedBookings));
    }

    [HttpGet]
    public async Task<IActionResult> Presences(int? id, int? dayId)
    {
        id ??= _sessionState.Get<int>("ActivityId");

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.Groups)
            .Include(a => a.Bookings)
                .ThenInclude(b => b.Child)
            .Include(a => a.Bookings)
                .ThenInclude(b => b.Group)
            .Include(a => a.Bookings)
                .ThenInclude(b => b.Days)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        _sessionState.Set<int>("ActivityId", id.Value);

        dayId = SelectDefaultActivityDay(activity, dayId);
        var selectedDay = activity.Days.FirstOrDefault(d => d.DayId == dayId);
        var dayOptions = BuildDayDropdownOptions(activity, dayId);
        var children = BuildChildrenList(activity, dayId);

        var viewModel = new PresencesViewModel
        {
            Activity = activity,
            SelectedActivityDayId = dayId,
            SelectedActivityDay = selectedDay,
            ActivityDayOptions = dayOptions,
            Children = children
        };

        return View(viewModel);
    }

    private int? SelectDefaultActivityDay(Activity activity, int? dayId)
    {
        if (dayId != null)
            return dayId;

        var today = DateTime.Today;
        var todayDay = activity.Days.FirstOrDefault(d => d.IsActive && d.DayDate.Date == today);

        if (todayDay != null)
            return todayDay.DayId;

        var firstDay = activity.Days.Where(d => d.IsActive).OrderBy(d => d.DayDate).FirstOrDefault();
        return firstDay?.DayId;
    }

    private List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> BuildDayDropdownOptions(Activity activity, int? selectedDayId)
    {
        return activity.Days
            .Where(d => d.IsActive)
            .OrderBy(d => d.DayDate)
            .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = d.DayId.ToString(),
                Text = $"{d.Label} - {d.DayDate:dd/MM/yyyy}",
                Selected = d.DayId == selectedDayId
            })
            .ToList();
    }

    private List<PresenceChildInfo> BuildChildrenList(Activity activity, int? dayId)
    {
        return activity.Bookings
            .Where(b => b.IsConfirmed)
            .Select(b =>
            {
                var bookingDay = b.Days.FirstOrDefault(bd => bd.ActivityDayId == dayId);
                return new PresenceChildInfo
                {
                    BookingId = b.Id,
                    ChildId = b.ChildId,
                    ChildFirstName = b.Child.FirstName,
                    ChildLastName = b.Child.LastName,
                    IsReserved = bookingDay?.IsReserved ?? false,
                    IsPresent = bookingDay?.IsPresent ?? false,
                    BookingDayId = bookingDay?.Id,
                    ActivityGroupName = b.Group?.Label
                };
            })
            .OrderBy(c => c.ChildLastName)
            .ThenBy(c => c.ChildFirstName)
            .ToList();
    }

    private Dictionary<Core.Entities.ActivityGroup, List<PresenceChildInfo>> BuildChildrenByGroup(Activity activity, int? dayId)
    {
        var childrenByGroup = new Dictionary<Core.Entities.ActivityGroup, List<PresenceChildInfo>>();

        foreach (var group in activity.Groups.OrderBy(g => g.Label))
        {
            var children = activity.Bookings
                .Where(b => b.IsConfirmed && b.GroupId == group.Id)
                .Select(b =>
                {
                    var bookingDay = b.Days.FirstOrDefault(bd => bd.ActivityDayId == dayId);
                    return new PresenceChildInfo
                    {
                        BookingId = b.Id,
                        ChildId = b.ChildId,
                        ChildFirstName = b.Child.FirstName,
                        ChildLastName = b.Child.LastName,
                        IsReserved = bookingDay?.IsReserved ?? false,
                        IsPresent = bookingDay?.IsPresent ?? false,
                        BookingDayId = bookingDay?.Id
                    };
                })
                .OrderBy(c => c.ChildLastName)
                .ThenBy(c => c.ChildFirstName)
                .ToList();

            if (children.Any())
            {
                childrenByGroup[group] = children;
            }
        }

        return childrenByGroup;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("BeginPresences")]
    public IActionResult PresencesPost(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        _sessionState.Set<int>("ActivityId", id);
        return RedirectToAction(nameof(Presences));
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePresence(int bookingDayId, bool isPresent)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var bookingDay = await _context.BookingDays.FindAsync(bookingDayId);

        if (bookingDay == null)
            return Json(new { success = false, message = _localizer["Message.BookingDayNotFound"].Value });

        bookingDay.IsPresent = isPresent;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> SendEmail(int? id)
    {
        id ??= _sessionState.Get<int>("ActivityId");

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .Include(a => a.Days)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID for future visits
        _sessionState.Set<int>("ActivityId", id.Value);

        // Load excursions for this activity
        var excursions = await _context.Excursions
            .Where(e => e.ActivityId == id)
            .ToListAsync();

        ViewBag.Templates = await _templateService.GetAllTemplatesAsync(activity.OrganisationId);

        var viewModel = new SendEmailViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            RecipientOptions = GetRecipientOptions(activity.Groups, excursions),
            DayOptions = GetDayOptions(activity.Days)
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(SendEmailViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await RepopulateViewModelAsync(model, ct);
            return View(model);
        }

        var recipientGroupId = ExtractRecipientGroupId(model.SelectedRecipient);
        var (attachmentFileName, attachmentFilePath) = await SaveAttachmentAsync(model.AttachmentFile, ct);

        try
        {
            var organisationId = await _context.Activities
                .Where(a => a.Id == model.ActivityId)
                .Select(a => a.OrganisationId)
                .FirstOrDefaultAsync(ct);
            var organisation = await _context.Organisations.FirstOrDefaultAsync(o => o.Id == organisationId, ct);

            _logger.LogInformation("Starting email sending process for activity {ActivityId}, recipient: {Recipient}",
                model.ActivityId, model.SelectedRecipient);

            int emailsSentCount;

            if (model.SendSeparateEmailPerChild)
            {
                _logger.LogInformation("Sending separate email per child");
                // 1 email per child: replace variables per booking
                emailsSentCount = await SendPerChildAsync(model, recipientGroupId, organisation!, attachmentFilePath, ct);
            }
            else
            {
                _logger.LogInformation("Sending email per parent");
                // 1 email per parent: send same message to unique parent emails
                emailsSentCount = await SendPerParentAsync(model, recipientGroupId, attachmentFilePath, ct);
            }

            if (emailsSentCount == 0)
            {
                ModelState.AddModelError(string.Empty, _localizer["Message.NoRecipientsFound"]);
                await RepopulateViewModelAsync(model, ct);
                return View(model);
            }

            // Log the sent email
            var allEmails = await _emailRecipientService.GetRecipientEmailsAsync(
                model.ActivityId, model.SelectedRecipient, recipientGroupId, model.SelectedDayId, ct);
            await LogSentEmailAsync(model, recipientGroupId, allEmails, attachmentFileName, attachmentFilePath, ct);

            TempData[TempDataSuccessMessage] = string.Format(_localizer["Message.EmailSent"].Value, emailsSentCount);
            return RedirectToAction(nameof(SendEmail));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email for activity {ActivityId}", model.ActivityId);
            ModelState.AddModelError(string.Empty, $"{_localizer["Message.EmailSendError"]}: {ex.Message}");
            await RepopulateViewModelAsync(model, ct);
            return View(model);
        }
    }

    /// <summary>
    /// Sends one personalized email per child booking, replacing variables with booking-specific data
    /// </summary>
    private async Task<int> SendPerChildAsync(
        SendEmailViewModel model,
        int? recipientGroupId,
        Organisation organisation,
        string? attachmentFilePath,
        CancellationToken ct)
    {
        var bookings = await GetFilteredBookingsAsync(model.ActivityId, model.SelectedRecipient, recipientGroupId, model.SelectedDayId, ct);

        int count = 0;
        foreach (var booking in bookings)
        {
            var personalizedSubject = _variableReplacementService.ReplaceVariables(model.Subject, booking, organisation);
            var personalizedMessage = _variableReplacementService.ReplaceVariables(model.Message, booking, organisation);

            await _emailService.SendEmailAsync(
                new List<string> { booking.Child.Parent.Email },
                personalizedSubject,
                personalizedMessage,
                attachmentFilePath);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Sends one email per unique parent (no variable replacement, or uses first child's data)
    /// </summary>
    private async Task<int> SendPerParentAsync(
        SendEmailViewModel model,
        int? recipientGroupId,
        string? attachmentFilePath,
        CancellationToken ct)
    {
        var recipientEmails = await _emailRecipientService.GetRecipientEmailsAsync(
            model.ActivityId, model.SelectedRecipient, recipientGroupId, model.SelectedDayId, ct);

        if (!recipientEmails.Any())
            return 0;

        // Send same message to all unique parent emails (HTML content used as-is from TinyMCE)
        foreach (var email in recipientEmails)
        {
            await _emailService.SendEmailAsync(
                new List<string> { email },
                model.Subject,
                model.Message,
                attachmentFilePath);
        }

        return recipientEmails.Count;
    }

    /// <summary>
    /// Gets bookings matching the filters, with navigation properties loaded for variable replacement
    /// </summary>
    private async Task<List<Booking>> GetFilteredBookingsAsync(
        int activityId,
        string selectedRecipient,
        int? recipientGroupId,
        int? scheduledDayId,
        CancellationToken ct)
    {
        var query = _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .Include(b => b.Group)
            .Include(b => b.Days)
            .Where(b => b.ActivityId == activityId && b.IsConfirmed);

        // Apply day filter
        if (scheduledDayId.HasValue)
        {
            query = query.Where(b => b.Days.Any(bd => bd.ActivityDayId == scheduledDayId.Value && bd.IsReserved));
        }

        // Apply recipient type filter
        if (selectedRecipient == RecipientMedicalSheetReminder)
        {
            query = query.Where(b => !b.IsMedicalSheet);
        }
        else if (selectedRecipient == RecipientUnpaidParents)
        {
            // Filter bookings where PaidAmount < TotalAmount (unpaid balance)
            query = query.Where(b => b.PaidAmount < b.TotalAmount);
        }
        else if (selectedRecipient.StartsWith(RecipientGroupPrefix) && recipientGroupId.HasValue)
        {
            query = query.Where(b => b.GroupId == recipientGroupId);
        }
        else if (selectedRecipient.StartsWith(RecipientExcursionPrefix))
        {
            // Extract excursion ID and filter bookings registered to that excursion
            var excursionIdStr = selectedRecipient.Substring(RecipientExcursionPrefix.Length);
            if (int.TryParse(excursionIdStr, out var excursionId))
            {
                var registeredBookingIds = await _context.ExcursionRegistrations
                    .Where(er => er.ExcursionId == excursionId)
                    .Select(er => er.BookingId)
                    .ToListAsync(ct);

                query = query.Where(b => registeredBookingIds.Contains(b.Id));
            }
        }

        return await query.ToListAsync(ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("BeginSendEmail")]
    public IActionResult SendEmailPost(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        _sessionState.Set<int>("ActivityId", id);
        return RedirectToAction(nameof(SendEmail));
    }

    [HttpGet]
    public async Task<IActionResult> SentEmails(int? id)
    {
        id ??= _sessionState.Get<int>("ActivityId");

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID for future visits
        _sessionState.Set<int>("ActivityId", id.Value);

        var sentEmails = await _context.EmailsSent
            .Where(e => e.ActivityId == id)
            .OrderByDescending(e => e.SentDate)
            .ToListAsync();

        var viewModel = new SentEmailsViewModel
        {
            Activity = activity,
            SentEmails = sentEmails
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("BeginSentEmails")]
    public IActionResult SentEmailsPost(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        _sessionState.Set<int>("ActivityId", id);
        return RedirectToAction(nameof(SentEmails));
    }

    [HttpGet]
    public async Task<IActionResult> TeamMembers(int? id)
    {
        id ??= _sessionState.Get<int>("ActivityId");

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.TeamMembers)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID for future visits
        _sessionState.Set<int>("ActivityId", id.Value);

        // Get all team members for this organisation
        var allTeamMembers = await _context.TeamMembers
            .Where(tm => tm.OrganisationId == activity.OrganisationId)
            .ToListAsync();

        // Get assigned team member IDs
        var assignedIds = new HashSet<int>(activity.TeamMembers.Select(tm => tm.TeamMemberId));

        // Filter available team members
        var availableTeamMembers = allTeamMembers.Where(tm => !assignedIds.Contains(tm.TeamMemberId));

        var viewModel = new TeamMembersViewModel
        {
            Activity = activity,
            AssignedTeamMembers = activity.TeamMembers,
            AvailableTeamMembers = availableTeamMembers
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("BeginTeamMembers")]
    public IActionResult TeamMembersPost(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        _sessionState.Set<int>("ActivityId", id);
        return RedirectToAction(nameof(TeamMembers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTeamMember(int id, int teamMemberId)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(TeamMembers));
        }

        var activity = await _context.Activities
            .Include(a => a.TeamMembers)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        var teamMember = await _context.TeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamMemberId == teamMemberId
                && tm.OrganisationId == activity.OrganisationId);

        if (teamMember == null)
        {
            TempData[TempDataErrorMessage] = _localizer["Message.TeamMemberNotFound"].Value;
            return RedirectToAction(nameof(TeamMembers));
        }

        if (!activity.TeamMembers.Any(tm => tm.TeamMemberId == teamMemberId))
        {
            activity.TeamMembers.Add(teamMember);
            await _context.SaveChangesAsync();
            TempData[TempDataSuccessMessage] = string.Format(_localizer["Message.TeamMemberAdded"].Value, teamMember.FirstName, teamMember.LastName);
        }

        return RedirectToAction(nameof(TeamMembers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTeamMember(int id, int teamMemberId)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(TeamMembers));
        }

        var activity = await _context.Activities
            .Include(a => a.TeamMembers)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        var teamMember = activity.TeamMembers.FirstOrDefault(tm => tm.TeamMemberId == teamMemberId);

        if (teamMember != null)
        {
            activity.TeamMembers.Remove(teamMember);
            await _context.SaveChangesAsync();
            TempData[TempDataSuccessMessage] = string.Format(_localizer["Message.TeamMemberRemoved"].Value, teamMember.FirstName, teamMember.LastName);
        }

        return RedirectToAction(nameof(TeamMembers));
    }

    private List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GetRecipientOptions(
        IEnumerable<Core.Entities.ActivityGroup> groups,
        IEnumerable<Core.Entities.Excursion> excursions)
    {
        var options = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
        {
            new() { Value = RecipientAllParents, Text = _localizer["Email.RecipientAllParents"] },
            new() { Value = RecipientMedicalSheetReminder, Text = _localizer["Email.RecipientMedicalSheetReminder"] },
            new() { Value = RecipientUnpaidParents, Text = _localizer["Email.RecipientUnpaidParents"] }
        };

        // Add groups section
        if (groups.Any())
        {
            options.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = "",
                Text = "──────────────────────────",
                Disabled = true
            });

            foreach (var group in groups)
            {
                options.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = $"{RecipientGroupPrefix}{group.Id}",
                    Text = $"{_localizer["Email.RecipientGroup"]}: {group.Label}"
                });
            }
        }

        // Add excursions section
        var activeExcursions = excursions.Where(e => e.IsActive).ToList();
        if (activeExcursions.Any())
        {
            options.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = "",
                Text = "──────────────────────────",
                Disabled = true
            });

            foreach (var excursion in activeExcursions)
            {
                options.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = $"{RecipientExcursionPrefix}{excursion.Id}",
                    Text = $"{_localizer["Email.RecipientExcursion"]}: {excursion.Name}"
                });
            }
        }

        return options;
    }

    private async Task RepopulateViewModelAsync(SendEmailViewModel model, CancellationToken ct)
    {
        var activity = await _context.Activities
            .Include(a => a.Groups)
            .Include(a => a.Days)
            .FirstOrDefaultAsync(a => a.Id == model.ActivityId, ct);

        if (activity != null)
        {
            var excursions = await _context.Excursions
                .Where(e => e.ActivityId == model.ActivityId)
                .ToListAsync(ct);

            model.RecipientOptions = GetRecipientOptions(activity.Groups, excursions);
            model.DayOptions = GetDayOptions(activity.Days);
            ViewBag.Templates = await _templateService.GetAllTemplatesAsync(activity.OrganisationId);
        }
    }

    private static List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GetDayOptions(IEnumerable<ActivityDay> days)
    {
        return days
            .Where(d => d.IsActive)
            .OrderBy(d => d.DayDate)
            .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = d.DayId.ToString(),
                Text = $"{d.Label} - {d.DayDate:dd/MM/yyyy}"
            })
            .ToList();
    }

    private static int? ExtractRecipientGroupId(string selectedRecipient)
    {
        if (selectedRecipient.StartsWith(RecipientGroupPrefix) &&
            int.TryParse(selectedRecipient.Substring(RecipientGroupPrefix.Length), out var groupId))
        {
            return groupId;
        }
        return null;
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

    private async Task LogSentEmailAsync(
        SendEmailViewModel model,
        int? recipientGroupId,
        IEnumerable<string> recipientEmails,
        string? attachmentFileName,
        string? attachmentFilePath,
        CancellationToken ct)
    {
        var recipientType = model.SelectedRecipient switch
        {
            var r when r == RecipientAllParents => EmailRecipient.AllParents,
            var r when r == RecipientMedicalSheetReminder => EmailRecipient.MedicalSheetReminder,
            var r when r.StartsWith(RecipientGroupPrefix) => EmailRecipient.ActivityGroup,
            _ => EmailRecipient.AllParents
        };

        var emailSent = new EmailSent
        {
            ActivityId = model.ActivityId,
            RecipientType = recipientType,
            RecipientGroupId = recipientGroupId,
            ScheduledDayId = model.SelectedDayId,
            RecipientEmails = string.Join("; ", recipientEmails),
            Subject = model.Subject,
            Message = model.Message,
            SendSeparateEmailPerChild = model.SendSeparateEmailPerChild,
            AttachmentFileName = attachmentFileName,
            AttachmentFilePath = attachmentFilePath,
            SentDate = DateTime.Now
        };

        _context.EmailsSent.Add(emailSent);
        await _context.SaveChangesAsync(ct);
    }

    // GET: ActivityManagement/Print
    public async Task<IActionResult> Print(int activityId, int dayId)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId);

        var activityDay = await _context.ActivityDays
            .FirstOrDefaultAsync(d => d.DayId == dayId);

        if (activity == null || activityDay == null)
        {
            return NotFound();
        }

        var bookings = await _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Group)
            .Include(b => b.Days)
            .Where(b => b.ActivityId == activityId && b.IsConfirmed)
            .ToListAsync();

        var presenceItems = new List<PresenceChildInfo>();

        foreach (var booking in bookings)
        {
            var bookingDay = booking.Days.FirstOrDefault(bd => bd.ActivityDayId == dayId);

            if (bookingDay != null && bookingDay.IsReserved)
            {
                presenceItems.Add(new PresenceChildInfo
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

        var viewModel = new PrintPresencesViewModel
        {
            Activity = activity,
            ActivityDay = activityDay,
            PresenceItems = presenceItems
                .OrderBy(p => p.ChildLastName)
                .ThenBy(p => p.ChildFirstName)
                .ToList()
        };

        return View(viewModel);
    }

    /// <summary>
    /// Sets the selected activity ID in both session and persistent cookie
    /// </summary>

    // GET: ActivityManagement/GroupAssignment
    public async Task<IActionResult> GroupAssignment()
    {
        var selectedActivityId = _sessionState.Get<int>("ActivityId");
        if (selectedActivityId == null)
        {
            TempData[TempDataErrorMessage] = _localizer["ActivityManagement.SelectActivity"].Value;
            return RedirectToAction(nameof(Index));
        }

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == selectedActivityId.Value);

        if (activity == null)
        {
            return NotFound();
        }

        // Debug: log all bookings first
        var allBookings = await _context.Bookings
            .Include(b => b.Child)
            .Where(b => b.ActivityId == selectedActivityId.Value)
            .ToListAsync();
        _logger.LogInformation("GroupAssignment DEBUG - Activity {ActivityId}: Total {Count} bookings. Details: {Details}",
            selectedActivityId.Value,
            allBookings.Count,
            string.Join(" | ", allBookings.Select(b => $"ID={b.Id}, Child={b.Child?.FirstName ?? "NULL"}, GroupId={b.GroupId?.ToString() ?? "NULL"}, IsConfirmed={b.IsConfirmed}")));

        // Get all confirmed bookings without a real group (either null or "Sans groupe")
        var unassignedBookings = await _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Group)
            .Where(b => b.ActivityId == selectedActivityId.Value
                     && b.IsConfirmed
                     && (b.GroupId == null || (b.Group != null && b.Group.Label == "Sans groupe")))
            .OrderBy(b => b.Child.LastName)
            .ThenBy(b => b.Child.FirstName)
            .ToListAsync();

        _logger.LogInformation("GroupAssignment: Found {Count} unassigned bookings (IsConfirmed=true, GroupId=null OR Group='Sans groupe')",
            unassignedBookings.Count);

        var viewModel = new GroupAssignmentViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            UnassignedChildren = unassignedBookings.Select(b => new UnassignedChildViewModel
            {
                BookingId = b.Id,
                ChildId = b.ChildId,
                FirstName = b.Child.FirstName,
                LastName = b.Child.LastName,
                BirthDate = b.Child.BirthDate
            }).ToList(),
            GroupOptions = activity.Groups
                .OrderBy(g => g.Label)
                .Select(g => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Label
                })
                .ToList()
        };

        return View(viewModel);
    }

    // POST: ActivityManagement/AssignToGroup
    [HttpPost]
    public async Task<IActionResult> AssignToGroup([FromBody] AssignToGroupRequest request)
    {
        try
        {
            var booking = await _context.Bookings.FindAsync(request.BookingId);
            if (booking == null)
            {
                return NotFound(new { success = false, message = _localizer["Message.BookingNotFound"].Value });
            }

            var group = await _context.ActivityGroups.FindAsync(request.GroupId);
            if (group == null)
            {
                return NotFound(new { success = false, message = _localizer["Message.GroupNotFound"].Value });
            }

            booking.GroupId = request.GroupId;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = _localizer["Message.GroupAssigned"].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning booking {BookingId} to group {GroupId}", request.BookingId, request.GroupId);
            return StatusCode(500, new { success = false, message = _localizer["Message.ErrorOccurred"].Value });
        }
    }

    // POST: ActivityManagement/BeginManageBookings
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("BeginManageBookings")]
    public IActionResult ManageBookingsPost(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        _sessionState.Set<int>("ActivityId", id);
        return RedirectToAction(nameof(ManageBookings));
    }

    // GET: ActivityManagement/ManageBookings
    public async Task<IActionResult> ManageBookings()
    {
        var selectedActivityId = _sessionState.Get<int>("ActivityId");
        if (selectedActivityId == null)
        {
            TempData[TempDataErrorMessage] = _localizer["ActivityManagement.SelectActivity"].Value;
            return RedirectToAction(nameof(Index));
        }

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == selectedActivityId.Value);

        if (activity == null)
        {
            return NotFound();
        }

        // Get all bookings that need attention (not confirmed OR no real group OR no medical sheet)
        var bookings = await _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Group)
            .Where(b => b.ActivityId == selectedActivityId.Value
                     && (!b.IsConfirmed
                         || b.GroupId == null
                         || (b.Group != null && b.Group.Label == "Sans groupe")
                         || !b.IsMedicalSheet))
            .OrderBy(b => b.Child.LastName)
            .ThenBy(b => b.Child.FirstName)
            .ToListAsync();

        var viewModel = new ManageBookingsViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            Bookings = bookings.Select(b => new BookingManagementItem
            {
                BookingId = b.Id,
                ChildId = b.ChildId,
                FirstName = b.Child.FirstName,
                LastName = b.Child.LastName,
                BirthDate = b.Child.BirthDate,
                IsConfirmed = b.IsConfirmed,
                GroupId = b.GroupId,
                GroupLabel = b.Group?.Label,
                IsMedicalSheet = b.IsMedicalSheet
            }).ToList(),
            GroupOptions = activity.Groups
                .Where(g => g.Label != "Sans groupe")
                .OrderBy(g => g.Label)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Label
                })
                .ToList()
        };

        // Calculate summary counts
        viewModel.PendingConfirmationCount = viewModel.Bookings.Count(b => b.NeedsConfirmation);
        viewModel.WithoutGroupCount = viewModel.Bookings.Count(b => b.NeedsGroup);
        viewModel.WithoutMedicalSheetCount = viewModel.Bookings.Count(b => b.NeedsMedicalSheet);

        return View(viewModel);
    }

    // POST: ActivityManagement/UpdateBooking
    [HttpPost]
    public async Task<IActionResult> UpdateBooking([FromBody] UpdateBookingRequest request)
    {
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Group)
                .FirstOrDefaultAsync(b => b.Id == request.BookingId);

            if (booking == null)
            {
                return NotFound(new { success = false, message = _localizer["Message.BookingNotFound"].Value });
            }

            // Update the requested field(s)
            bool updated = false;

            if (request.GroupId.HasValue)
            {
                var group = await _context.ActivityGroups.FindAsync(request.GroupId.Value);
                if (group == null)
                {
                    return NotFound(new { success = false, message = _localizer["Message.GroupNotFound"].Value });
                }
                booking.GroupId = request.GroupId.Value;
                updated = true;
            }

            if (request.IsConfirmed.HasValue)
            {
                booking.IsConfirmed = request.IsConfirmed.Value;

                // If confirming and no group assigned, create/assign "Sans groupe"
                if (request.IsConfirmed.Value && booking.GroupId == null)
                {
                    var activity = await _context.Activities
                        .Include(a => a.Groups)
                        .FirstOrDefaultAsync(a => a.Id == booking.ActivityId);

                    if (activity != null)
                    {
                        var noGroup = activity.Groups.FirstOrDefault(g => g.Label == "Sans groupe");
                        if (noGroup == null)
                        {
                            noGroup = new ActivityGroup
                            {
                                ActivityId = activity.Id,
                                Label = "Sans groupe",
                                Capacity = null
                            };
                            _context.ActivityGroups.Add(noGroup);
                            await _context.SaveChangesAsync();
                        }
                        booking.GroupId = noGroup.Id;
                    }
                }
                updated = true;
            }

            if (request.IsMedicalSheet.HasValue)
            {
                booking.IsMedicalSheet = request.IsMedicalSheet.Value;
                updated = true;
            }

            if (updated)
            {
                await _context.SaveChangesAsync();
            }

            // Check if booking is now complete (confirmed, has real group, has medical sheet)
            var isComplete = booking.IsConfirmed
                          && booking.GroupId.HasValue
                          && (booking.Group == null || booking.Group.Label != "Sans groupe")
                          && booking.IsMedicalSheet;

            return Ok(new
            {
                success = true,
                message = _localizer["Message.BookingUpdated"].Value,
                isComplete = isComplete
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking {BookingId}", request.BookingId);
            return StatusCode(500, new { success = false, message = _localizer["Message.ErrorOccurred"].Value });
        }
    }

    // GET: ActivityManagement/GetManageBookingsStats
    [HttpGet]
    public async Task<IActionResult> GetManageBookingsStats(int activityId)
    {
        var bookings = await _context.Bookings
            .Include(b => b.Group)
            .Where(b => b.ActivityId == activityId
                     && (!b.IsConfirmed
                         || b.GroupId == null
                         || (b.Group != null && b.Group.Label == "Sans groupe")
                         || !b.IsMedicalSheet))
            .ToListAsync();

        var stats = new
        {
            pendingConfirmation = bookings.Count(b => !b.IsConfirmed),
            withoutGroup = bookings.Count(b => b.GroupId == null || (b.Group != null && b.Group.Label == "Sans groupe")),
            withoutMedicalSheet = bookings.Count(b => !b.IsMedicalSheet)
        };

        return Ok(stats);
    }

    public class UpdateBookingRequest
    {
        public int BookingId { get; set; }
        public int? GroupId { get; set; }
        public bool? IsConfirmed { get; set; }
        public bool? IsMedicalSheet { get; set; }
    }

    public class AssignToGroupRequest
    {
        public int BookingId { get; set; }
        public int GroupId { get; set; }
    }
}
