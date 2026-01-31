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
public class ActivityManagementController : Controller
{
    private const string SessionActivityId = "Activity_Id";
    private const string CookieActivityId = "SelectedActivityId";
    private const string RecipientAllParents = "allparents";
    private const string RecipientMedicalSheetReminder = "medicalsheetreminder";
    private const string RecipientGroupPrefix = "group_";
    private const string TempDataSuccessMessage = "SuccessMessage";
    private const string TempDataErrorMessage = "ErrorMessage";

    private readonly CedevaDbContext _context;
    private readonly ILogger<ActivityManagementController> _logger;
    private readonly IEmailService _emailService;
    private readonly IEmailRecipientService _emailRecipientService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ActivityManagementController(
        CedevaDbContext context,
        ILogger<ActivityManagementController> logger,
        IEmailService emailService,
        IEmailRecipientService emailRecipientService,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _emailRecipientService = emailRecipientService;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (id is null)
        {
            // Try to get from session first
            var idStr = HttpContext.Session.GetString(SessionActivityId);

            // If not in session, try to restore from persistent cookie
            if (string.IsNullOrEmpty(idStr))
            {
                idStr = Request.Cookies[CookieActivityId];

                // Restore session from cookie
                if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var cookieParsed))
                {
                    HttpContext.Session.SetString(SessionActivityId, cookieParsed.ToString());
                    id = cookieParsed;
                }
            }
            else if (int.TryParse(idStr, out var sessionParsed))
            {
                id = sessionParsed;
            }
        }

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.Groups)
            .Include(a => a.Bookings)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID in session and cookie for future visits
        SetSelectedActivityId(id.Value);

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

        SetSelectedActivityId(id);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> UnconfirmedBookings(int? id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (id is null)
        {
            // Try to get from session first
            var idStr = HttpContext.Session.GetString(SessionActivityId);

            // If not in session, try to restore from persistent cookie
            if (string.IsNullOrEmpty(idStr))
            {
                idStr = Request.Cookies[CookieActivityId];

                // Restore session from cookie
                if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var cookieParsed))
                {
                    HttpContext.Session.SetString(SessionActivityId, cookieParsed.ToString());
                    id = cookieParsed;
                }
            }
            else if (int.TryParse(idStr, out var sessionParsed))
            {
                id = sessionParsed;
            }
        }

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .Include(a => a.Bookings)
                .ThenInclude(b => b.Child)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID in session and cookie for future visits
        SetSelectedActivityId(id.Value);

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

        SetSelectedActivityId(id);
        return RedirectToAction(nameof(UnconfirmedBookings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmBooking(int bookingId, int groupId)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(UnconfirmedBookings));
        }

        var booking = await _context.Bookings
            .Include(b => b.Activity)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return NotFound();

        if (groupId <= 0)
        {
            ModelState.AddModelError(string.Empty, _localizer["Message.SelectGroup"]);
            return RedirectToAction(nameof(UnconfirmedBookings));
        }

        booking.GroupId = groupId;
        booking.IsConfirmed = true;

        await _context.SaveChangesAsync();

        TempData[TempDataSuccessMessage] = _localizer["Message.BookingConfirmed"].Value;
        return RedirectToAction(nameof(UnconfirmedBookings));
    }

    [HttpGet]
    public async Task<IActionResult> Presences(int? id, int? dayId)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (id is null)
        {
            // Try to get from session first
            var idStr = HttpContext.Session.GetString(SessionActivityId);

            // If not in session, try to restore from persistent cookie
            if (string.IsNullOrEmpty(idStr))
            {
                idStr = Request.Cookies[CookieActivityId];

                // Restore session from cookie
                if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var cookieParsed))
                {
                    HttpContext.Session.SetString(SessionActivityId, cookieParsed.ToString());
                    id = cookieParsed;
                }
            }
            else if (int.TryParse(idStr, out var sessionParsed))
            {
                id = sessionParsed;
            }
        }

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

        // Store the activity ID in session and cookie for future visits
        SetSelectedActivityId(id.Value);

        // If no day is selected, take today's active day, or fallback to first active day
        if (dayId == null)
        {
            var today = DateTime.Today;
            var todayDay = activity.Days.FirstOrDefault(d => d.IsActive && d.DayDate.Date == today);

            if (todayDay != null)
            {
                dayId = todayDay.DayId;
            }
            else
            {
                var firstDay = activity.Days.Where(d => d.IsActive).OrderBy(d => d.DayDate).FirstOrDefault();
                dayId = firstDay?.DayId;
            }
        }

        var selectedDay = activity.Days.FirstOrDefault(d => d.DayId == dayId);

        // Create options for the day dropdown
        var dayOptions = activity.Days
            .Where(d => d.IsActive)
            .OrderBy(d => d.DayDate)
            .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = d.DayId.ToString(),
                Text = $"{d.Label} - {d.DayDate:dd/MM/yyyy}",
                Selected = d.DayId == dayId
            })
            .ToList();

        // Group children by activity group
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

        var viewModel = new PresencesViewModel
        {
            Activity = activity,
            SelectedActivityDayId = dayId,
            SelectedActivityDay = selectedDay,
            ActivityDayOptions = dayOptions,
            ChildrenByGroup = childrenByGroup
        };

        return View(viewModel);
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

        SetSelectedActivityId(id);
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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (id is null)
        {
            // Try to get from session first
            var idStr = HttpContext.Session.GetString(SessionActivityId);

            // If not in session, try to restore from persistent cookie
            if (string.IsNullOrEmpty(idStr))
            {
                idStr = Request.Cookies[CookieActivityId];

                // Restore session from cookie
                if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var cookieParsed))
                {
                    HttpContext.Session.SetString(SessionActivityId, cookieParsed.ToString());
                    id = cookieParsed;
                }
            }
            else if (int.TryParse(idStr, out var sessionParsed))
            {
                id = sessionParsed;
            }
        }

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID in session and cookie for future visits
        SetSelectedActivityId(id.Value);

        var viewModel = new SendEmailViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            RecipientOptions = GetRecipientOptions(activity.Groups)
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(SendEmailViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await RepopulateRecipientOptionsAsync(model, ct);
            return View(model);
        }

        var recipientGroupId = ExtractRecipientGroupId(model.SelectedRecipient);
        var recipientEmails = await _emailRecipientService.GetRecipientEmailsAsync(
            model.ActivityId,
            model.SelectedRecipient,
            recipientGroupId,
            ct);

        if (!recipientEmails.Any())
        {
            ModelState.AddModelError(string.Empty, _localizer["Message.NoRecipientsFound"]);
            await RepopulateRecipientOptionsAsync(model, ct);
            return View(model);
        }

        var (attachmentFileName, attachmentFilePath) = await SaveAttachmentAsync(model.AttachmentFile, ct);
        var htmlContent = $"<p>{model.Message.Replace("\n", "<br/>")}</p>";

        try
        {
            await _emailService.SendEmailAsync(recipientEmails, model.Subject, htmlContent, attachmentFilePath);
            await LogSentEmailAsync(model, recipientGroupId, recipientEmails, attachmentFileName, attachmentFilePath, ct);

            TempData[TempDataSuccessMessage] = string.Format(_localizer["Message.EmailSent"].Value, recipientEmails.Count);
            return RedirectToAction(nameof(SendEmail));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email for activity {ActivityId}", model.ActivityId);
            ModelState.AddModelError(string.Empty, _localizer["Message.EmailSendError"]);
            await RepopulateRecipientOptionsAsync(model, ct);
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

        SetSelectedActivityId(id);
        return RedirectToAction(nameof(SendEmail));
    }

    [HttpGet]
    public async Task<IActionResult> SentEmails(int? id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (id is null)
        {
            // Try to get from session first
            var idStr = HttpContext.Session.GetString(SessionActivityId);

            // If not in session, try to restore from persistent cookie
            if (string.IsNullOrEmpty(idStr))
            {
                idStr = Request.Cookies[CookieActivityId];

                // Restore session from cookie
                if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var cookieParsed))
                {
                    HttpContext.Session.SetString(SessionActivityId, cookieParsed.ToString());
                    id = cookieParsed;
                }
            }
            else if (int.TryParse(idStr, out var sessionParsed))
            {
                id = sessionParsed;
            }
        }

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID in session and cookie for future visits
        SetSelectedActivityId(id.Value);

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

        SetSelectedActivityId(id);
        return RedirectToAction(nameof(SentEmails));
    }

    [HttpGet]
    public async Task<IActionResult> TeamMembers(int? id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (id is null)
        {
            // Try to get from session first
            var idStr = HttpContext.Session.GetString(SessionActivityId);

            // If not in session, try to restore from persistent cookie
            if (string.IsNullOrEmpty(idStr))
            {
                idStr = Request.Cookies[CookieActivityId];

                // Restore session from cookie
                if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var cookieParsed))
                {
                    HttpContext.Session.SetString(SessionActivityId, cookieParsed.ToString());
                    id = cookieParsed;
                }
            }
            else if (int.TryParse(idStr, out var sessionParsed))
            {
                id = sessionParsed;
            }
        }

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.TeamMembers)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        // Store the activity ID in session and cookie for future visits
        SetSelectedActivityId(id.Value);

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

        SetSelectedActivityId(id);
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

    private List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GetRecipientOptions(IEnumerable<Core.Entities.ActivityGroup> groups)
    {
        var options = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
        {
            new() { Value = RecipientAllParents, Text = _localizer["Email.RecipientAllParents"] },
            new() { Value = RecipientMedicalSheetReminder, Text = _localizer["Email.RecipientMedicalSheetReminder"] }
        };

        foreach (var group in groups)
        {
            options.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = $"{RecipientGroupPrefix}{group.Id}",
                Text = $"{_localizer["Email.RecipientGroup"]}: {group.Label}"
            });
        }

        return options;
    }

    private async Task RepopulateRecipientOptionsAsync(SendEmailViewModel model, CancellationToken ct)
    {
        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == model.ActivityId, ct);

        if (activity != null)
        {
            model.RecipientOptions = GetRecipientOptions(activity.Groups);
        }
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
        var uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName;
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
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
            RecipientEmails = string.Join("; ", recipientEmails),
            Subject = model.Subject,
            Message = model.Message,
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
                .OrderBy(p => p.ActivityGroupName)
                .ThenBy(p => p.ChildLastName)
                .ThenBy(p => p.ChildFirstName)
                .ToList()
        };

        return View(viewModel);
    }

    /// <summary>
    /// Sets the selected activity ID in both session and persistent cookie
    /// </summary>
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
