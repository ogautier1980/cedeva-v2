using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.QuestionTemplates.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.QuestionTemplates;

/// <summary>
/// CRUD for an organisation's default question templates (Lot J — "modèle de questions par
/// activité"). Copied into each new activity's own <see cref="ActivityQuestion"/> rows at creation
/// time (<see cref="IQuestionTemplateService"/>); editing a template afterwards never touches
/// activities already created from it, same as the Lot E locked e-mail templates.
/// </summary>
[Authorize]
public class QuestionTemplatesController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public QuestionTemplatesController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index()
    {
        var templates = await _context.OrganisationQuestionTemplates
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();
        return View(templates);
    }

    [HttpGet]
    public IActionResult Create()
    {
        PopulateQuestionTypes();
        return View(new QuestionTemplateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuestionTemplateViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            PopulateQuestionTypes();
            return View(viewModel);
        }

        var orgId = _currentUserService.OrganisationId ?? 0;

        _context.OrganisationQuestionTemplates.Add(new OrganisationQuestionTemplate
        {
            OrganisationId = orgId,
            QuestionText = viewModel.QuestionText.Trim(),
            QuestionType = viewModel.QuestionType,
            IsRequired = viewModel.IsRequired,
            Options = string.IsNullOrWhiteSpace(viewModel.Options) ? null : viewModel.Options.Trim(),
            DisplayOrder = viewModel.DisplayOrder
        });
        await _context.SaveChangesAsync();

        return this.RedirectToIndexWithSuccess(_localizer["QuestionTemplate.Created"].Value);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var template = await _context.OrganisationQuestionTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template == null)
            return NotFound();

        PopulateQuestionTypes();
        return View(new QuestionTemplateViewModel
        {
            Id = template.Id,
            QuestionText = template.QuestionText,
            QuestionType = template.QuestionType,
            IsRequired = template.IsRequired,
            Options = template.Options,
            DisplayOrder = template.DisplayOrder
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(QuestionTemplateViewModel viewModel)
    {
        var template = await _context.OrganisationQuestionTemplates.FirstOrDefaultAsync(t => t.Id == viewModel.Id);
        if (template == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            PopulateQuestionTypes();
            return View(viewModel);
        }

        template.QuestionText = viewModel.QuestionText.Trim();
        template.QuestionType = viewModel.QuestionType;
        template.IsRequired = viewModel.IsRequired;
        template.Options = string.IsNullOrWhiteSpace(viewModel.Options) ? null : viewModel.Options.Trim();
        template.DisplayOrder = viewModel.DisplayOrder;
        await _context.SaveChangesAsync();

        return this.RedirectToIndexWithSuccess(_localizer["QuestionTemplate.Updated"].Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _context.OrganisationQuestionTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template != null)
        {
            _context.OrganisationQuestionTemplates.Remove(template);
            await _context.SaveChangesAsync();
            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["QuestionTemplate.Deleted"].Value;
        }
        return RedirectToAction(nameof(Index));
    }

    private void PopulateQuestionTypes()
    {
        var questionTypes = Enum.GetValues<QuestionType>()
            .Select(qt => new { Value = (int)qt, Text = _localizer[$"Enum.QuestionType.{qt}"] })
            .ToList();
        ViewBag.QuestionTypes = new SelectList(questionTypes, "Value", "Text");
    }
}
