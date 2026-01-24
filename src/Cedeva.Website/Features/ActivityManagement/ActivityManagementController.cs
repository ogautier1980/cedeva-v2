using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.ActivityManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Website.Features.ActivityManagement;

[Authorize]
public class ActivityManagementController : Controller
{
    private const string SessionActivityId = "Activity_Id";

    private readonly CedevaDbContext _context;
    private readonly ILogger<ActivityManagementController> _logger;
    private readonly IEmailService _emailService;
    private readonly IEmailRecipientService _emailRecipientService;

    public ActivityManagementController(
        CedevaDbContext context,
        ILogger<ActivityManagementController> logger,
        IEmailService emailService,
        IEmailRecipientService emailRecipientService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _emailRecipientService = emailRecipientService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        if (id is null)
        {
            var idStr = HttpContext.Session.GetString(SessionActivityId);
            if (int.TryParse(idStr, out var parsed))
            {
                id = parsed;
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
        HttpContext.Session.SetString(SessionActivityId, id.ToString());

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> UnconfirmedBookings(int? id)
    {
        if (id is null)
        {
            var idStr = HttpContext.Session.GetString(SessionActivityId);
            if (int.TryParse(idStr, out var parsed))
            {
                id = parsed;
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
        HttpContext.Session.SetString(SessionActivityId, id.ToString());
        return RedirectToAction(nameof(UnconfirmedBookings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmBooking(int bookingId, int groupId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Activity)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return NotFound();

        if (groupId <= 0)
        {
            ModelState.AddModelError(string.Empty, "Veuillez sélectionner un groupe.");
            return RedirectToAction(nameof(UnconfirmedBookings), new { id = booking.ActivityId });
        }

        booking.GroupId = groupId;
        booking.IsConfirmed = true;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Inscription confirmée avec succès.";
        return RedirectToAction(nameof(UnconfirmedBookings), new { id = booking.ActivityId });
    }

    [HttpGet]
    public async Task<IActionResult> Presences(int? id, int? dayId)
    {
        if (id is null)
        {
            var idStr = HttpContext.Session.GetString(SessionActivityId);
            if (int.TryParse(idStr, out var parsed))
            {
                id = parsed;
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

        // Si aucun jour n'est sélectionné, prendre le premier jour actif
        if (dayId == null)
        {
            var firstDay = activity.Days.Where(d => d.IsActive).OrderBy(d => d.DayDate).FirstOrDefault();
            dayId = firstDay?.DayId;
        }

        var selectedDay = activity.Days.FirstOrDefault(d => d.DayId == dayId);

        // Créer les options pour le dropdown des jours
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

        // Grouper les enfants par groupe
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
        HttpContext.Session.SetString(SessionActivityId, id.ToString());
        return RedirectToAction(nameof(Presences));
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePresence(int bookingDayId, bool isPresent)
    {
        var bookingDay = await _context.BookingDays.FindAsync(bookingDayId);

        if (bookingDay == null)
            return Json(new { success = false, message = "Jour d'inscription introuvable." });

        bookingDay.IsPresent = isPresent;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> SendEmail(int? id)
    {
        if (id is null)
        {
            var idStr = HttpContext.Session.GetString(SessionActivityId);
            if (int.TryParse(idStr, out var parsed))
            {
                id = parsed;
            }
        }

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        var viewModel = new SendEmailViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            RecipientOptions = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
            {
                new() { Value = "allparents", Text = "Tous les parents (enfants inscrits à l'activité)" },
                new() { Value = "medicalsheetreminder", Text = "Rappel fiche médicale (enfants sans fiche médicale)" }
            }
        };

        foreach (var group in activity.Groups)
        {
            viewModel.RecipientOptions.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = $"group_{group.Id}",
                Text = $"Groupe : {group.Label}"
            });
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("BeginSendEmail")]
    public IActionResult SendEmailPost(int id)
    {
        HttpContext.Session.SetString(SessionActivityId, id.ToString());
        return RedirectToAction(nameof(SendEmail));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(SendEmailViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var activity = await _context.Activities
                .Include(a => a.Groups)
                .FirstOrDefaultAsync(a => a.Id == model.ActivityId, ct);

            if (activity != null)
            {
                model.RecipientOptions = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
                {
                    new() { Value = "allparents", Text = "Tous les parents (enfants inscrits à l'activité)" },
                    new() { Value = "medicalsheetreminder", Text = "Rappel fiche médicale (enfants sans fiche médicale)" }
                };

                foreach (var group in activity.Groups)
                {
                    model.RecipientOptions.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = $"group_{group.Id}",
                        Text = $"Groupe : {group.Label}"
                    });
                }
            }

            return View(model);
        }

        // Extract recipient type and group ID
        int? recipientGroupId = null;
        if (model.SelectedRecipient.StartsWith("group_"))
        {
            if (int.TryParse(model.SelectedRecipient.Substring(6), out var groupId))
            {
                recipientGroupId = groupId;
            }
        }

        // Get recipient emails
        var recipientEmails = await _emailRecipientService.GetRecipientEmailsAsync(
            model.ActivityId,
            model.SelectedRecipient,
            recipientGroupId,
            ct);

        if (!recipientEmails.Any())
        {
            ModelState.AddModelError(string.Empty, "Aucun destinataire trouvé pour ce critère.");

            var activity = await _context.Activities
                .Include(a => a.Groups)
                .FirstOrDefaultAsync(a => a.Id == model.ActivityId, ct);

            if (activity != null)
            {
                model.RecipientOptions = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
                {
                    new() { Value = "allparents", Text = "Tous les parents (enfants inscrits à l'activité)" },
                    new() { Value = "medicalsheetreminder", Text = "Rappel fiche médicale (enfants sans fiche médicale)" }
                };

                foreach (var group in activity.Groups)
                {
                    model.RecipientOptions.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = $"group_{group.Id}",
                        Text = $"Groupe : {group.Label}"
                    });
                }
            }

            return View(model);
        }

        // Handle file attachment
        string? attachmentFileName = null;
        string? attachmentFilePath = null;

        if (model.AttachmentFile != null && model.AttachmentFile.Length > 0)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "attachments");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            attachmentFileName = Path.GetFileName(model.AttachmentFile.FileName);
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + attachmentFileName;
            attachmentFilePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(attachmentFilePath, FileMode.Create))
            {
                await model.AttachmentFile.CopyToAsync(fileStream, ct);
            }
        }

        // Construct HTML email content
        var htmlContent = $"<p>{model.Message.Replace("\n", "<br/>")}</p>";

        // Send emails
        try
        {
            await _emailService.SendEmailAsync(recipientEmails, model.Subject, htmlContent, attachmentFilePath);

            // Log sent email
            var recipientType = model.SelectedRecipient switch
            {
                "allparents" => EmailRecipient.AllParents,
                "medicalsheetreminder" => EmailRecipient.MedicalSheetReminder,
                _ when model.SelectedRecipient.StartsWith("group_") => EmailRecipient.ActivityGroup,
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

            TempData["SuccessMessage"] = $"Email envoyé avec succès à {recipientEmails.Count} destinataire(s).";
            return RedirectToAction(nameof(SendEmail), new { id = model.ActivityId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email for activity {ActivityId}", model.ActivityId);
            ModelState.AddModelError(string.Empty, "Une erreur est survenue lors de l'envoi de l'email.");

            var activity = await _context.Activities
                .Include(a => a.Groups)
                .FirstOrDefaultAsync(a => a.Id == model.ActivityId, ct);

            if (activity != null)
            {
                model.RecipientOptions = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
                {
                    new() { Value = "allparents", Text = "Tous les parents (enfants inscrits à l'activité)" },
                    new() { Value = "medicalsheetreminder", Text = "Rappel fiche médicale (enfants sans fiche médicale)" }
                };

                foreach (var group in activity.Groups)
                {
                    model.RecipientOptions.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = $"group_{group.Id}",
                        Text = $"Groupe : {group.Label}"
                    });
                }
            }

            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> SentEmails(int? id)
    {
        if (id is null)
        {
            var idStr = HttpContext.Session.GetString(SessionActivityId);
            if (int.TryParse(idStr, out var parsed))
            {
                id = parsed;
            }
        }

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

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
        HttpContext.Session.SetString(SessionActivityId, id.ToString());
        return RedirectToAction(nameof(SentEmails));
    }
}
