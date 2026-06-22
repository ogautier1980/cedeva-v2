using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.ActivityManagement.ViewModels;
using Cedeva.Website.Infrastructure;
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
    private const string SessionKeyActivityId = "ActivityId";
    private const string DefaultGroupLabel = "Sans groupe";
    private const string LocalizerKeyErrorOccurred = "Message.ErrorOccurred";

    private readonly CedevaDbContext _context;
    private readonly ILogger<ActivityManagementController> _logger;
    private readonly IEmailFacadeService _emailServices;
    private readonly IActivityEmailService _activityEmailService;
    private readonly ISessionStateService _sessionState;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ActivityManagementController(
        CedevaDbContext context,
        ILogger<ActivityManagementController> logger,
        IEmailFacadeService emailServices,
        IActivityEmailService activityEmailService,
        ISessionStateService sessionState,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _logger = logger;
        _emailServices = emailServices;
        _activityEmailService = activityEmailService;
        _sessionState = sessionState;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        // Use service to get or set activity ID
        id ??= _sessionState.Get<int>(SessionKeyActivityId);

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
        _sessionState.Set<int>(SessionKeyActivityId, id.Value);

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

        _sessionState.Set<int>(SessionKeyActivityId, id);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> UnconfirmedBookings(int? id)
    {
        id ??= _sessionState.Get<int>(SessionKeyActivityId);

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
        _sessionState.Set<int>(SessionKeyActivityId, id.Value);

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

        _sessionState.Set<int>(SessionKeyActivityId, id);
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

        _sessionState.Set<int>(SessionKeyActivityId, id);
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
            var noGroupLabel = DefaultGroupLabel;
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

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.BookingConfirmed"].Value;
        return RedirectToAction(nameof(UnconfirmedBookings));
    }

    [HttpGet]
    public async Task<IActionResult> Presences(int? id, int? dayId)
    {
        id ??= _sessionState.Get<int>(SessionKeyActivityId);

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

        _sessionState.Set<int>(SessionKeyActivityId, id.Value);

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

    private static int? SelectDefaultActivityDay(Activity activity, int? dayId)
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

    private static List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> BuildDayDropdownOptions(Activity activity, int? selectedDayId)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;

        return activity.Days
            .Where(d => d.IsActive)
            .OrderBy(d => d.DayDate)
            .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = d.DayId.ToString(),
                Text = $"{char.ToUpper(d.DayDate.ToString("dddd", culture)[0])}{d.DayDate.ToString("dddd", culture).Substring(1)} {d.DayDate:dd-MM}",
                Selected = d.DayId == selectedDayId
            })
            .ToList();
    }

    private static List<PresenceChildInfo> BuildChildrenList(Activity activity, int? dayId)
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("BeginPresences")]
    public IActionResult PresencesPost(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        _sessionState.Set<int>(SessionKeyActivityId, id);
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
        id ??= _sessionState.Get<int>(SessionKeyActivityId);

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .Include(a => a.Days)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID for future visits
        _sessionState.Set<int>(SessionKeyActivityId, id.Value);

        // Load excursions for this activity
        var excursions = await _context.Excursions
            .Where(e => e.ActivityId == id)
            .ToListAsync();

        ViewBag.Templates = await _emailServices.Template.GetAllTemplatesAsync(activity.OrganisationId, activity.Id);

        var viewModel = new SendEmailViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            RecipientOptions = GetRecipientOptions(activity.Groups, excursions, await GetContactGroupsAsync(activity.OrganisationId, default)),
            DayOptions = GetDayOptions(activity.Days),
            ContactOptions = await GetContactOptionsAsync(activity.OrganisationId, default)
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

        var (attachmentFileName, attachmentFilePath) = await SaveAttachmentAsync(model.AttachmentFile, ct);

        try
        {
            var result = await _activityEmailService.SendAsync(new ActivityEmailRequest(
                model.ActivityId, model.SelectedRecipient, model.SelectedDayId,
                model.Subject, model.Message, model.SendSeparateEmailPerChild,
                model.SelectedContactEmails ?? new List<string>(),
                attachmentFileName, attachmentFilePath), ct);

            switch (result.Outcome)
            {
                case ActivityEmailOutcome.NoContactsSelected:
                    ModelState.AddModelError(string.Empty, _localizer["Message.NoContactsSelected"]);
                    await RepopulateViewModelAsync(model, ct);
                    return View(model);

                case ActivityEmailOutcome.NoRecipients:
                    ModelState.AddModelError(string.Empty, _localizer["Message.NoRecipientsFound"]);
                    await RepopulateViewModelAsync(model, ct);
                    return View(model);

                default:
                    TempData[ControllerExtensions.SuccessMessageKey] = string.Format(_localizer["Message.EmailSent"].Value, result.SentCount);
                    return RedirectToAction(nameof(SendEmail));
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while sending email for activity {ActivityId}", model.ActivityId);
            ModelState.AddModelError(string.Empty, $"{_localizer["Message.EmailSendError"]}: {ex.Message}");
            await RepopulateViewModelAsync(model, ct);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while sending email for activity {ActivityId}", model.ActivityId);
            ModelState.AddModelError(string.Empty, $"{_localizer["Message.EmailSendError"]}: {ex.Message}");
            await RepopulateViewModelAsync(model, ct);
            return View(model);
        }
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

        _sessionState.Set<int>(SessionKeyActivityId, id);
        return RedirectToAction(nameof(SendEmail));
    }

    [HttpGet]
    public async Task<IActionResult> SentEmails(int? id)
    {
        id ??= _sessionState.Get<int>(SessionKeyActivityId);

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID for future visits
        _sessionState.Set<int>(SessionKeyActivityId, id.Value);

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

        _sessionState.Set<int>(SessionKeyActivityId, id);
        return RedirectToAction(nameof(SentEmails));
    }

    [HttpGet]
    public async Task<IActionResult> TeamMembers(int? id)
    {
        id ??= _sessionState.Get<int>(SessionKeyActivityId);

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.TeamMembers)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID for future visits
        _sessionState.Set<int>(SessionKeyActivityId, id.Value);

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

        _sessionState.Set<int>(SessionKeyActivityId, id);
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
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Message.TeamMemberNotFound"].Value;
            return RedirectToAction(nameof(TeamMembers));
        }

        if (!activity.TeamMembers.Any(tm => tm.TeamMemberId == teamMemberId))
        {
            activity.TeamMembers.Add(teamMember);
            await _context.SaveChangesAsync();
            TempData[ControllerExtensions.SuccessMessageKey] = string.Format(_localizer["Message.TeamMemberAdded"].Value, teamMember.FirstName, teamMember.LastName);
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
            TempData[ControllerExtensions.SuccessMessageKey] = string.Format(_localizer["Message.TeamMemberRemoved"].Value, teamMember.FirstName, teamMember.LastName);
        }

        return RedirectToAction(nameof(TeamMembers));
    }

    private List<SelectListItem> GetRecipientOptions(
        IEnumerable<Core.Entities.ActivityGroup> groups,
        IEnumerable<Core.Entities.Excursion> excursions,
        IEnumerable<Core.Entities.ContactGroup> contactGroups)
    {
        var generalGroup = new SelectListGroup { Name = _localizer["Email.GeneralRecipients"] };

        var options = new List<SelectListItem>
        {
            new() { Value = EmailRecipientKeys.AllParents, Text = _localizer["Email.RecipientAllParents"], Group = generalGroup },
            new() { Value = EmailRecipientKeys.MedicalSheetReminder, Text = _localizer["Email.RecipientMedicalSheetReminder"], Group = generalGroup },
            new() { Value = EmailRecipientKeys.UnpaidParents, Text = _localizer["Email.RecipientUnpaidParents"], Group = generalGroup },
            new() { Value = EmailRecipientKeys.CustomContacts, Text = _localizer["Email.RecipientCustomContacts"], Group = generalGroup }
        };

        // Add groups section
        if (groups.Any())
        {
            var groupsListGroup = new SelectListGroup { Name = _localizer["Email.GroupRecipients"] };

            foreach (var group in groups)
            {
                options.Add(new SelectListItem
                {
                    Value = $"{EmailRecipientKeys.GroupPrefix}{group.Id}",
                    Text = group.Label,
                    Group = groupsListGroup
                });
            }
        }

        // Add excursions section
        var activeExcursions = excursions.Where(e => e.IsActive).ToList();
        if (activeExcursions.Any())
        {
            var excursionsListGroup = new SelectListGroup { Name = _localizer["Email.ExcursionRecipients"] };

            foreach (var excursion in activeExcursions)
            {
                options.Add(new SelectListItem
                {
                    Value = $"{EmailRecipientKeys.ExcursionPrefix}{excursion.Id}",
                    Text = excursion.Name,
                    Group = excursionsListGroup
                });
            }
        }

        // Add saved contact groups section
        var savedGroups = contactGroups.ToList();
        if (savedGroups.Any())
        {
            var contactGroupsListGroup = new SelectListGroup { Name = _localizer["Email.ContactGroupRecipients"] };

            foreach (var cg in savedGroups)
            {
                options.Add(new SelectListItem
                {
                    Value = $"{EmailRecipientKeys.ContactGroupPrefix}{cg.Id}",
                    Text = cg.Name,
                    Group = contactGroupsListGroup
                });
            }
        }

        return options;
    }

    private async Task<List<Core.Entities.ContactGroup>> GetContactGroupsAsync(int organisationId, CancellationToken ct) =>
        await _context.ContactGroups
            .Where(g => g.OrganisationId == organisationId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

    /// <summary>
    /// Builds the selectable contacts for the custom email-group picker: every organisation contact
    /// that has an email — parents, team members and "other contacts" — de-duplicated by email.
    /// </summary>
    private async Task<List<ContactSelectItem>> GetContactOptionsAsync(int organisationId, CancellationToken ct)
    {
        var parents = await _context.Parents
            .Where(p => p.OrganisationId == organisationId && p.Email != "")
            .Select(p => new ContactSelectItem { Email = p.Email, Display = p.LastName + ", " + p.FirstName, Category = _localizer["Contacts.Parent"].Value })
            .ToListAsync(ct);
        var teamMembers = await _context.TeamMembers
            .Where(t => t.OrganisationId == organisationId && t.Email != "")
            .Select(t => new ContactSelectItem { Email = t.Email, Display = t.LastName + ", " + t.FirstName, Category = _localizer["Contacts.Animator"].Value })
            .ToListAsync(ct);
        var others = await _context.Contacts
            .Where(c => c.OrganisationId == organisationId && c.Email != null && c.Email != "")
            .Select(c => new ContactSelectItem { Email = c.Email!, Display = c.LastName + ", " + c.FirstName, Category = _localizer["Contacts.Others"].Value })
            .ToListAsync(ct);

        return parents.Concat(teamMembers).Concat(others)
            .GroupBy(c => c.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(c => c.Category).ThenBy(c => c.Display)
            .ToList();
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

            model.RecipientOptions = GetRecipientOptions(activity.Groups, excursions, await GetContactGroupsAsync(activity.OrganisationId, ct));
            model.DayOptions = GetDayOptions(activity.Days);
            model.ContactOptions = await GetContactOptionsAsync(activity.OrganisationId, ct);
            ViewBag.Templates = await _emailServices.Template.GetAllTemplatesAsync(activity.OrganisationId, activity.Id);
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
        var selectedActivityId = _sessionState.Get<int>(SessionKeyActivityId);
        if (selectedActivityId == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["ActivityManagement.SelectActivity"].Value;
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
                     && (b.GroupId == null || (b.Group != null && b.Group.Label == DefaultGroupLabel)))
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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while assigning booking {BookingId} to group {GroupId}", request.BookingId, request.GroupId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while assigning booking {BookingId} to group {GroupId}", request.BookingId, request.GroupId);
            return StatusCode(500, new { success = false, message = _localizer[LocalizerKeyErrorOccurred].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while assigning booking {BookingId} to group {GroupId}", request.BookingId, request.GroupId);
            return StatusCode(500, new { success = false, message = _localizer[LocalizerKeyErrorOccurred].Value });
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

        _sessionState.Set<int>(SessionKeyActivityId, id);
        return RedirectToAction(nameof(ManageBookings));
    }

    // GET: ActivityManagement/ManageBookings
    public async Task<IActionResult> ManageBookings()
    {
        var selectedActivityId = _sessionState.Get<int>(SessionKeyActivityId);
        if (selectedActivityId == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["ActivityManagement.SelectActivity"].Value;
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
                         || (b.Group != null && b.Group.Label == DefaultGroupLabel)
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
                .Where(g => g.Label != DefaultGroupLabel)
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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
            var (updated, groupNotFound) = await ApplyBookingUpdatesAsync(booking, request);

            if (groupNotFound)
            {
                return NotFound(new { success = false, message = _localizer["Message.GroupNotFound"].Value });
            }

            if (updated)
            {
                await _context.SaveChangesAsync();
            }

            // Check if booking is now complete (confirmed, has real group, has medical sheet)
            var isComplete = booking.IsConfirmed
                          && booking.GroupId.HasValue
                          && (booking.Group == null || booking.Group.Label != DefaultGroupLabel)
                          && booking.IsMedicalSheet;

            return Ok(new
            {
                success = true,
                message = _localizer["Message.BookingUpdated"].Value,
                isComplete = isComplete
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while updating booking {BookingId}", request.BookingId);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while updating booking {BookingId}", request.BookingId);
            return StatusCode(500, new { success = false, message = _localizer[LocalizerKeyErrorOccurred].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating booking {BookingId}", request.BookingId);
            return StatusCode(500, new { success = false, message = _localizer[LocalizerKeyErrorOccurred].Value });
        }
    }

    private async Task<(bool updated, bool groupNotFound)> ApplyBookingUpdatesAsync(Booking booking, UpdateBookingRequest request)
    {
        bool updated = false;

        if (request.GroupId.HasValue)
        {
            var group = await _context.ActivityGroups.FindAsync(request.GroupId.Value);
            if (group == null)
            {
                return (false, true);
            }
            booking.GroupId = request.GroupId.Value;
            updated = true;
        }

        if (request.IsConfirmed.HasValue)
        {
            booking.IsConfirmed = request.IsConfirmed.Value;
            if (request.IsConfirmed.Value && booking.GroupId == null)
            {
                await AssignDefaultGroupAsync(booking);
            }
            updated = true;
        }

        if (request.IsMedicalSheet.HasValue)
        {
            booking.IsMedicalSheet = request.IsMedicalSheet.Value;
            updated = true;
        }

        return (updated, false);
    }

    private async Task AssignDefaultGroupAsync(Booking booking)
    {
        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == booking.ActivityId);

        if (activity == null) return;

        var noGroup = activity.Groups.FirstOrDefault(g => g.Label == DefaultGroupLabel);
        if (noGroup == null)
        {
            noGroup = new ActivityGroup
            {
                ActivityId = activity.Id,
                Label = DefaultGroupLabel,
                Capacity = null
            };
            _context.ActivityGroups.Add(noGroup);
            await _context.SaveChangesAsync();
        }
        booking.GroupId = noGroup.Id;
    }

    // GET: ActivityManagement/GetManageBookingsStats
    [HttpGet]
    public async Task<IActionResult> GetManageBookingsStats(int activityId)
    {
        // Tenant isolation: the Activities DbSet is filtered by the multi-tenancy query filter
        // (admin bypasses it). If the activity isn't visible to the current user, return empty
        // stats rather than leaking another organisation's booking counts.
        var activityIsVisible = await _context.Activities.AnyAsync(a => a.Id == activityId);
        if (!activityIsVisible)
        {
            return Ok(new { pendingConfirmation = 0, withoutGroup = 0, withoutMedicalSheet = 0 });
        }

        var bookings = await _context.Bookings
            .Include(b => b.Group)
            .Where(b => b.ActivityId == activityId
                     && (!b.IsConfirmed
                         || b.GroupId == null
                         || (b.Group != null && b.Group.Label == DefaultGroupLabel)
                         || !b.IsMedicalSheet))
            .ToListAsync();

        var stats = new
        {
            pendingConfirmation = bookings.Count(b => !b.IsConfirmed),
            withoutGroup = bookings.Count(b => b.GroupId == null || (b.Group != null && b.Group.Label == DefaultGroupLabel)),
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
