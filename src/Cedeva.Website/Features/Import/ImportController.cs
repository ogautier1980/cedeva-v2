using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Import.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Import;

/// <summary>
/// Generic CSV import surface for several entity types. Tenancy is enforced here: the target
/// organisation for org-scoped imports is the logged-in coordinator's own organisation (or, for an
/// admin, an explicitly chosen one) — never a value read from the uploaded file.
/// </summary>
[Authorize]
public class ImportController : Controller
{
    private readonly IEnumerable<ICsvEntityImporter> _importers;
    private readonly ICurrentUserService _currentUserService;
    private readonly CedevaDbContext _context;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        IEnumerable<ICsvEntityImporter> importers,
        ICurrentUserService currentUserService,
        CedevaDbContext context,
        IStringLocalizer<SharedResources> localizer,
        ILogger<ImportController> logger)
    {
        _importers = importers;
        _currentUserService = currentUserService;
        _context = context;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? type = null)
    {
        var vm = await BuildIndexAsync(type);
        // If a non-admin selected an admin-only type, ignore it.
        if (vm.Selected?.AdminOnly == true && !vm.IsAdmin)
            vm.SelectedType = null;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> Index(string type, IFormFile? csvFile, int? targetOrganisationId, CancellationToken ct)
    {
        var importer = _importers.FirstOrDefault(i => i.Key == type);
        var isAdmin = _currentUserService.IsAdmin;

        if (importer == null)
            return NotFound();

        // Admin-only importers (e.g. organisations) are gated to admins.
        if (importer.AdminOnly && !isAdmin)
            return Forbid();

        // Resolve the target organisation server-side; never trust the CSV for tenancy.
        var organisationId = 0;
        if (!importer.AdminOnly)
        {
            if (isAdmin)
            {
                if (targetOrganisationId is null || !await _context.Organisations.IgnoreQueryFilters().AnyAsync(o => o.Id == targetOrganisationId, ct))
                {
                    ModelState.AddModelError(string.Empty, _localizer["Import.SelectOrganisation"].Value);
                    return View(await BuildIndexAsync(type, targetOrganisationId));
                }
                organisationId = targetOrganisationId.Value;
            }
            else
            {
                organisationId = _currentUserService.OrganisationId ?? 0;
                if (organisationId == 0)
                    return Forbid();
            }
        }

        if (csvFile == null || csvFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, _localizer["Import.NoFile"].Value);
            return View(await BuildIndexAsync(type, targetOrganisationId));
        }
        if (!string.Equals(Path.GetExtension(csvFile.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, _localizer["Import.NotCsv"].Value);
            return View(await BuildIndexAsync(type, targetOrganisationId));
        }

        await using var stream = csvFile.OpenReadStream();
        var result = await importer.ImportAsync(stream, organisationId, ct);

        _logger.LogInformation("CSV import '{Type}' by {UserId} (org {Org}): {Created} created, {Skipped} skipped, {Errors} errors",
            type, _currentUserService.UserId, organisationId, result.Created, result.Skipped, result.Errors.Count);

        if (result.Created > 0)
            TempData[ControllerExtensions.SuccessMessageKey] = string.Format(_localizer["Import.SummaryGeneric"].Value, result.Created);

        ViewBag.TypeName = _localizer[importer.DisplayNameKey].Value;
        return View("ImportResult", result);
    }

    private async Task<ImportIndexViewModel> BuildIndexAsync(string? type, int? targetOrganisationId = null)
    {
        var isAdmin = _currentUserService.IsAdmin;

        var options = _importers
            .Where(i => isAdmin || !i.AdminOnly)
            .Select(i => new ImporterOption
            {
                Key = i.Key,
                DisplayName = _localizer[i.DisplayNameKey].Value,
                ColumnsTemplate = i.ColumnsTemplate,
                AdminOnly = i.AdminOnly
            })
            .OrderBy(o => o.DisplayName)
            .ToList();

        var vm = new ImportIndexViewModel
        {
            SelectedType = type,
            Importers = options,
            IsAdmin = isAdmin,
            TargetOrganisationId = targetOrganisationId
        };

        if (isAdmin)
        {
            vm.Organisations = await _context.Organisations.IgnoreQueryFilters()
                .OrderBy(o => o.Name)
                .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = o.Name })
                .ToListAsync();
        }

        return vm;
    }
}
