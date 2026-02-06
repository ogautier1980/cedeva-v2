using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.EmailTemplates.ViewModels;
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

    private const string SessionKeyActivityId = "EmailTemplates_ActivityId";
    private const string TempDataSuccessMessage = "SuccessMessage";
    private const string TempDataErrorMessage = "ErrorMessage";
    private const string ErrorGeneric = "Error.Generic";

    public EmailTemplatesController(
        IEmailTemplateService templateService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        UserManager<CedevaUser> userManager,
        CedevaDbContext context)
    {
        _templateService = templateService;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _userManager = userManager;
        _context = context;
    }

    public async Task<IActionResult> Index(EmailTemplateType? type = null, int? id = null)
    {
        if (id.HasValue)
            HttpContext.Session.SetInt32(SessionKeyActivityId, id.Value);

        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (activityId.HasValue)
        {
            var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == activityId.Value);
            if (activity != null)
            {
                ViewData["ActivityId"] = activity.Id;
                ViewData["ActivityName"] = activity.Name;
            }
        }
        ViewData["NavSection"] = "Emails";
        ViewData["NavAction"] = "EmailTemplates";

        var organisationId = _currentUserService.OrganisationId ?? 0;

        var templates = type.HasValue
            ? await _templateService.GetTemplatesByTypeAsync(type.Value, organisationId)
            : await _templateService.GetAllTemplatesAsync(organisationId);

        ViewBag.SelectedType = type;
        ViewBag.TypeOptions = GetTemplateTypeOptions();

        return View(templates);
    }

    public IActionResult Create()
    {
        var viewModel = new EmailTemplateViewModel
        {
            TemplateTypeOptions = GetTemplateTypeOptions()
        };

        return View(viewModel);
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

        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData[TempDataErrorMessage] = _localizer["Error.Unauthorized"].ToString();
                return RedirectToAction(nameof(Index));
            }

            var template = new EmailTemplate
            {
                OrganisationId = _currentUserService.OrganisationId ?? 0,
                Name = viewModel.Name,
                TemplateType = viewModel.TemplateType,
                Subject = viewModel.Subject,
                HtmlContent = viewModel.HtmlContent,
                IsDefault = viewModel.IsDefault,
                IsShared = viewModel.IsShared,
                CreatedBy = user.Id,
                CreatedAt = DateTime.UtcNow
            };

            await _templateService.CreateTemplateAsync(template);

            TempData[TempDataSuccessMessage] = _localizer["EmailTemplate.CreateSuccess"].ToString();
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData[TempDataErrorMessage] = _localizer[ErrorGeneric].ToString();
            viewModel.TemplateTypeOptions = GetTemplateTypeOptions();
            return View(viewModel);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            TempData[TempDataErrorMessage] = _localizer["Error.NotFound"].ToString();
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
            IsShared = template.IsShared,
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

        try
        {
            var template = await _templateService.GetTemplateByIdAsync(viewModel.Id);
            if (template == null)
            {
                TempData[TempDataErrorMessage] = _localizer["Error.NotFound"].ToString();
                return RedirectToAction(nameof(Index));
            }

            template.Name = viewModel.Name;
            template.TemplateType = viewModel.TemplateType;
            template.Subject = viewModel.Subject;
            template.HtmlContent = viewModel.HtmlContent;
            template.IsDefault = viewModel.IsDefault;
            template.IsShared = viewModel.IsShared;

            await _templateService.UpdateTemplateAsync(template);

            TempData[TempDataSuccessMessage] = _localizer["EmailTemplate.UpdateSuccess"].ToString();
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData[TempDataErrorMessage] = _localizer[ErrorGeneric].ToString();
            viewModel.TemplateTypeOptions = GetTemplateTypeOptions();
            return View(viewModel);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _templateService.DeleteTemplateAsync(id);
            TempData[TempDataSuccessMessage] = _localizer["EmailTemplate.DeleteSuccess"].ToString();
        }
        catch (Exception)
        {
            TempData[TempDataErrorMessage] = _localizer[ErrorGeneric].ToString();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id, EmailTemplateType type)
    {
        try
        {
            await _templateService.SetDefaultTemplateAsync(id, type);
            TempData[TempDataSuccessMessage] = _localizer["EmailTemplate.SetDefaultSuccess"].ToString();
        }
        catch (Exception)
        {
            TempData[TempDataErrorMessage] = _localizer[ErrorGeneric].ToString();
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Duplicate(int id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            TempData[TempDataErrorMessage] = _localizer["Error.NotFound"].ToString();
            return RedirectToAction(nameof(Index));
        }

        var viewModel = new EmailTemplateViewModel
        {
            Name = template.Name + " (Copy)",
            TemplateType = template.TemplateType,
            Subject = template.Subject,
            HtmlContent = template.HtmlContent,
            IsDefault = false, // Duplicate is never default
            IsShared = template.IsShared,
            TemplateTypeOptions = GetTemplateTypeOptions()
        };

        return View("Create", viewModel);
    }

    /// <summary>
    /// AJAX endpoint to get template data for loading into SendEmail form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTemplate(int id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            return NotFound();
        }

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
