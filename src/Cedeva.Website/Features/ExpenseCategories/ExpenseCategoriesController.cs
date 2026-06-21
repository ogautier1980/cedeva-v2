using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.ExpenseCategories.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.ExpenseCategories;

/// <summary>
/// CRUD for an organisation's manageable expense categories. Categories also grow on the fly when a
/// new name is typed in the expense form; this page lets coordinators rename or remove them.
/// </summary>
[Authorize]
public class ExpenseCategoriesController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ExpenseCategoriesController(
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
        var categories = await _context.ExpenseCategories.OrderBy(c => c.Name).ToListAsync();
        return View(categories);
    }

    [HttpGet]
    public IActionResult Create() => View(new ExpenseCategoryViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ExpenseCategoryViewModel viewModel)
    {
        var orgId = _currentUserService.OrganisationId ?? 0;
        var name = viewModel.Name.Trim();

        if (ModelState.IsValid && await _context.ExpenseCategories.AnyAsync(c => c.OrganisationId == orgId && c.Name == name))
            ModelState.AddModelError(nameof(viewModel.Name), _localizer["ExpenseCategory.Duplicate"].Value);

        if (!ModelState.IsValid)
            return View(viewModel);

        _context.ExpenseCategories.Add(new ExpenseCategory { OrganisationId = orgId, Name = name });
        await _context.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["ExpenseCategory.Created"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _context.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
            return NotFound();

        return View(new ExpenseCategoryViewModel { Id = category.Id, Name = category.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ExpenseCategoryViewModel viewModel)
    {
        var category = await _context.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == viewModel.Id);
        if (category == null)
            return NotFound();

        var orgId = category.OrganisationId;
        var newName = viewModel.Name.Trim();

        if (ModelState.IsValid && !string.Equals(newName, category.Name, StringComparison.OrdinalIgnoreCase)
            && await _context.ExpenseCategories.AnyAsync(c => c.OrganisationId == orgId && c.Name == newName))
            ModelState.AddModelError(nameof(viewModel.Name), _localizer["ExpenseCategory.Duplicate"].Value);

        if (!ModelState.IsValid)
            return View(viewModel);

        var oldName = category.Name;
        category.Name = newName;

        // Keep existing expenses consistent with the renamed category.
        if (!string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            var expenses = await _context.Expenses
                .Where(e => e.ActivityId != 0 && e.Category == oldName && e.Activity.OrganisationId == orgId)
                .ToListAsync();
            foreach (var e in expenses)
                e.Category = newName;
        }

        await _context.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["ExpenseCategory.Updated"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id);
        if (category != null)
        {
            _context.ExpenseCategories.Remove(category);
            await _context.SaveChangesAsync();
            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["ExpenseCategory.Deleted"].Value;
        }
        return RedirectToAction(nameof(Index));
    }
}
