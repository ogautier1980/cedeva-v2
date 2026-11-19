using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.FiscalYears;

/// <summary>
/// Admin: the childcare tax figures of each year (daily ceiling, tax code), printed on the fiscal
/// attestations of every organisation. They change every year (indexation).
/// </summary>
[Authorize(Roles = "Admin")]
public class FiscalYearsController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public FiscalYearsController(CedevaDbContext context, IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Years = await _context.FiscalYearSettings.OrderByDescending(s => s.Year).ToListAsync();
        var lastYear = DateTime.Today.Year - 1;
        return View(new FiscalYearSettingViewModel { Year = lastYear });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FiscalYearSettingViewModel viewModel)
    {
        if (ModelState.IsValid && await _context.FiscalYearSettings.AnyAsync(s => s.Year == viewModel.Year))
        {
            ModelState.AddModelError(nameof(viewModel.Year), _localizer["FiscalYear.Duplicate"].Value);
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Years = await _context.FiscalYearSettings.OrderByDescending(s => s.Year).ToListAsync();
            return View(nameof(Index), viewModel);
        }

        _context.FiscalYearSettings.Add(new FiscalYearSetting
        {
            Year = viewModel.Year,
            MaxDailyAmount = viewModel.MaxDailyAmount!.Value,
            TaxCode = viewModel.TaxCode.Trim()
        });
        await _context.SaveChangesAsync();
        return this.RedirectToIndexWithSuccess(_localizer["FiscalYear.Saved", viewModel.Year].Value);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var setting = await _context.FiscalYearSettings.FirstOrDefaultAsync(s => s.Id == id);
        if (setting == null) return NotFound();

        return View(new FiscalYearSettingViewModel
        {
            Id = setting.Id,
            Year = setting.Year,
            MaxDailyAmount = setting.MaxDailyAmount,
            TaxCode = setting.TaxCode
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(FiscalYearSettingViewModel viewModel)
    {
        var setting = await _context.FiscalYearSettings.FirstOrDefaultAsync(s => s.Id == viewModel.Id);
        if (setting == null) return NotFound();

        // The year itself is the key of the row: only its figures change here.
        ModelState.Remove(nameof(viewModel.Year));
        if (!ModelState.IsValid)
        {
            viewModel.Year = setting.Year;
            return View(viewModel);
        }

        setting.MaxDailyAmount = viewModel.MaxDailyAmount!.Value;
        setting.TaxCode = viewModel.TaxCode.Trim();
        await _context.SaveChangesAsync();
        return this.RedirectToIndexWithSuccess(_localizer["FiscalYear.Saved", setting.Year].Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var setting = await _context.FiscalYearSettings.FirstOrDefaultAsync(s => s.Id == id);
        if (setting != null)
        {
            _context.FiscalYearSettings.Remove(setting);
            await _context.SaveChangesAsync();
            this.SetSuccessMessage(_localizer["FiscalYear.Deleted", setting.Year].Value);
        }
        return RedirectToAction(nameof(Index));
    }
}

public class FiscalYearSettingViewModel
{
    public int Id { get; set; }

    [Range(2000, 2100, ErrorMessage = "Validation.Range")]
    [Display(Name = "FiscalYear.Year")]
    public int Year { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Range(0.01, 1000, ErrorMessage = "Validation.Range")]
    [Display(Name = "FiscalYear.MaxDailyAmount")]
    public decimal? MaxDailyAmount { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(10, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "FiscalYear.TaxCode")]
    public string TaxCode { get; set; } = "1384";
}
