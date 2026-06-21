using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.EmailTemplates.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.EmailTemplates;

[Authorize]
public class EmailTemplatesController : Controller
{
    private readonly IEmailTemplateService _templateService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly UserManager<CedevaUser> _userManager;
    private readonly CedevaDbContext _context;
    private readonly ILogger<EmailTemplatesController> _logger;

    private const string ErrorNotFound = "Error.NotFound";

    public EmailTemplatesController(
        IEmailTemplateService templateService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        UserManager<CedevaUser> userManager,
        CedevaDbContext context,
        ILogger<EmailTemplatesController> logger)
    {
        _templateService = templateService;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lists templates for a scope: an activity's templates when <paramref name="activityId"/> is
    /// given, otherwise the organisation-level library.
    /// </summary>
    public async Task<IActionResult> Index(int? activityId = null, EmailTemplateType? type = null)
    {
        var orgId = _currentUserService.OrganisationId ?? 0;

        if (activityId.HasValue)
        {
            var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == activityId.Value);
            if (activity == null)
            {
                TempData[ControllerExtensions.ErrorMessageKey] = _localizer[ErrorNotFound].ToString();
                return RedirectToAction(nameof(Index));
            }
            orgId = activity.OrganisationId;
            this.SetActivityViewData(activity.Id, activity.Name);
            ViewData["NavSection"] = "Emails";
            ViewData["NavAction"] = "EmailTemplates";
            ViewBag.ActivityName = activity.Name;
        }

        ViewBag.ActivityId = activityId;
        ViewBag.SelectedType = type;
        ViewBag.TypeOptions = GetTemplateTypeOptions();

        var templates = type.HasValue
            ? await _templateService.GetTemplatesByTypeAsync(type.Value, orgId, activityId)
            : await _templateService.GetAllTemplatesAsync(orgId, activityId);

        return View(templates);
    }

    public IActionResult Create(int? activityId = null)
    {
        return View(new EmailTemplateViewModel
        {
            ActivityId = activityId,
            TemplateTypeOptions = GetTemplateTypeOptions()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmailTemplateViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            viewModel.TemplateTypeOptions = GetTemplateTypeOptions();
            return View(viewModel);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.Unauthorized"].ToString();
            return RedirectToAction(nameof(Index), new { activityId = viewModel.ActivityId });
        }

        var template = new EmailTemplate
        {
            OrganisationId = _currentUserService.OrganisationId ?? 0,
            ActivityId = viewModel.ActivityId,
            Name = viewModel.Name,
            TemplateType = viewModel.TemplateType,
            Subject = viewModel.Subject,
            HtmlContent = viewModel.HtmlContent,
            IsDefault = viewModel.IsDefault,
            CreatedBy = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _templateService.CreateTemplateAsync(template);

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["EmailTemplate.CreateSuccess"].ToString();
        return RedirectToAction(nameof(Index), new { activityId = viewModel.ActivityId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[ErrorNotFound].ToString();
            return RedirectToAction(nameof(Index));
        }

        var viewModel = new EmailTemplateViewModel
        {
            Id = template.Id,
            Name = template.Name,
            TemplateType = template.TemplateType,
            Subject = template.Subject,
            HtmlContent = template.HtmlContent,
            IsDefault = template.IsDefault,
            ActivityId = template.ActivityId,
            TemplateTypeOptions = GetTemplateTypeOptions()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EmailTemplateViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            viewModel.TemplateTypeOptions = GetTemplateTypeOptions();
            return View(viewModel);
        }

        var template = await _templateService.GetTemplateByIdAsync(viewModel.Id);
        if (template == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[ErrorNotFound].ToString();
            return RedirectToAction(nameof(Index));
        }

        template.Name = viewModel.Name;
        template.TemplateType = viewModel.TemplateType;
        template.Subject = viewModel.Subject;
        template.HtmlContent = viewModel.HtmlContent;
        template.IsDefault = viewModel.IsDefault;

        await _templateService.UpdateTemplateAsync(template);

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["EmailTemplate.UpdateSuccess"].ToString();
        return RedirectToAction(nameof(Index), new { activityId = template.ActivityId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        var activityId = template?.ActivityId;

        await _templateService.DeleteTemplateAsync(id);
        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["EmailTemplate.DeleteSuccess"].ToString();
        return RedirectToAction(nameof(Index), new { activityId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id, EmailTemplateType type)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[ErrorNotFound].ToString();
            return RedirectToAction(nameof(Index));
        }

        await _templateService.SetDefaultTemplateAsync(id, type);
        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["EmailTemplate.SetDefaultSuccess"].ToString();
        return RedirectToAction(nameof(Index), new { activityId = template.ActivityId });
    }

    public async Task<IActionResult> Duplicate(int id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[ErrorNotFound].ToString();
            return RedirectToAction(nameof(Index));
        }

        var viewModel = new EmailTemplateViewModel
        {
            Name = template.Name + " (Copy)",
            TemplateType = template.TemplateType,
            Subject = template.Subject,
            HtmlContent = template.HtmlContent,
            IsDefault = false, // Duplicate is never default
            ActivityId = template.ActivityId,
            TemplateTypeOptions = GetTemplateTypeOptions()
        };

        return View("Create", viewModel);
    }

    /// <summary>Picker to import another activity's templates into this one.</summary>
    [HttpGet]
    public async Task<IActionResult> Import(int activityId)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == activityId);
        if (activity == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[ErrorNotFound].ToString();
            return RedirectToAction(nameof(Index));
        }

        var sources = await _templateService.GetActivitiesWithTemplatesAsync(activity.OrganisationId, excludeActivityId: activityId);

        return View(new ImportTemplatesViewModel
        {
            ActivityId = activityId,
            ActivityName = activity.Name,
            Sources = sources.Select(s => new SelectListItem
            {
                Value = s.ActivityId.ToString(),
                Text = $"{s.ActivityName} ({s.TemplateCount})"
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(int activityId, int sourceActivityId)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == activityId);
        if (activity == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[ErrorNotFound].ToString();
            return RedirectToAction(nameof(Index));
        }

        var count = await _templateService.ImportTemplatesFromActivityAsync(activity.OrganisationId, sourceActivityId, activityId);
        TempData[ControllerExtensions.SuccessMessageKey] = string.Format(_localizer["EmailTemplate.ImportSuccess"].Value, count);
        return RedirectToAction(nameof(Index), new { activityId });
    }

    /// <summary>
    /// Saves a composed email (from the activity SendEmail form) as a new activity-scoped template.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFromEmail(int activityId, string name, EmailTemplateType templateType, string subject, string message)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == activityId);
        if (activity == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[ErrorNotFound].ToString();
            return RedirectToAction("SendEmail", "ActivityManagement", new { id = activityId });
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["EmailTemplate.SaveFromEmailMissing"].ToString();
            return RedirectToAction("SendEmail", "ActivityManagement", new { id = activityId });
        }

        // CreatedBy/CreatedAt are populated by the auditing SaveChangesAsync interceptor.
        await _templateService.CreateTemplateAsync(new EmailTemplate
        {
            OrganisationId = activity.OrganisationId,
            ActivityId = activityId,
            Name = name.Trim(),
            TemplateType = templateType,
            Subject = subject.Trim(),
            HtmlContent = message,
            IsDefault = false
        });

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["EmailTemplate.SaveFromEmailSuccess"].ToString();
        return RedirectToAction("SendEmail", "ActivityManagement", new { id = activityId });
    }

    /// <summary>AJAX endpoint to get template data for loading into the SendEmail form.</summary>
    [HttpGet]
    public async Task<IActionResult> GetTemplate(int id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
            return NotFound();

        return Json(new
        {
            id = template.Id,
            name = template.Name,
            subject = template.Subject,
            htmlContent = template.HtmlContent
        });
    }

    private List<SelectListItem> GetTemplateTypeOptions()
    {
        return Enum.GetValues<EmailTemplateType>()
            .Select(t => new SelectListItem
            {
                Value = ((int)t).ToString(),
                Text = _localizer[$"EmailTemplateType.{t}"].ToString()
            })
            .ToList();
    }
}
