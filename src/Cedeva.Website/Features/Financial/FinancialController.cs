using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Financial.ViewModels;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Financial;

[Authorize(Roles = "Coordinator,Admin")]
public class FinancialController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICodaParserService _codaParserService;
    private readonly IBankReconciliationService _reconciliationService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILogger<FinancialController> _logger;

    private const string TempDataSuccess = "Success";
    private const string TempDataError = "Error";

    public FinancialController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        ICodaParserService codaParserService,
        IBankReconciliationService reconciliationService,
        IStringLocalizer<SharedResources> localizer,
        ILogger<FinancialController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _codaParserService = codaParserService;
        _reconciliationService = reconciliationService;
        _localizer = localizer;
        _logger = logger;
    }

    // GET: Financial/ImportCoda
    public async Task<IActionResult> ImportCoda()
    {
        var organisationId = _currentUserService.OrganisationId;

        // Charger la liste des fichiers CODA importés
        var codaFiles = await _context.CodaFiles
            .Where(cf => cf.OrganisationId == organisationId)
            .OrderByDescending(cf => cf.ImportDate)
            .Select(cf => new CodaFileListItemViewModel
            {
                Id = cf.Id,
                FileName = cf.FileName,
                ImportDate = cf.ImportDate,
                StatementDate = cf.StatementDate,
                AccountNumber = cf.AccountNumber,
                OldBalance = cf.OldBalance,
                NewBalance = cf.NewBalance,
                TransactionCount = cf.TransactionCount,
                ReconciledCount = cf.Transactions.Count(t => t.IsReconciled),
                UnreconciledCount = cf.Transactions.Count(t => !t.IsReconciled)
            })
            .ToListAsync();

        ViewBag.CodaFiles = codaFiles;

        return View(new ImportCodaViewModel());
    }

    // POST: Financial/ImportCoda
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportCoda(ImportCodaViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            return await ImportCoda(); // Recharge la liste
        }

        if (viewModel.CodaFile == null)
        {
            ModelState.AddModelError(nameof(ImportCodaViewModel.CodaFile), _localizer["Validation.FileRequired"].Value);
            return await ImportCoda();
        }

        // Valider l'extension du fichier
        var extension = Path.GetExtension(viewModel.CodaFile.FileName).ToLowerInvariant();
        if (extension != ".cod" && extension != ".txt")
        {
            ModelState.AddModelError(nameof(ImportCodaViewModel.CodaFile), _localizer["Validation.InvalidCodaFileExtension"].Value);
            return await ImportCoda();
        }

        try
        {
            var organisationId = _currentUserService.OrganisationId ?? throw new InvalidOperationException("Organisation ID not found");
            var userId = int.Parse(_currentUserService.UserId ?? throw new InvalidOperationException("User ID not found"));

            // Parser le fichier CODA
            CodaFileDto codaData;
            using (var stream = viewModel.CodaFile.OpenReadStream())
            {
                codaData = await _codaParserService.ParseCodaFileAsync(stream, viewModel.CodaFile.FileName);
            }

            // Importer dans la base de données
            var codaFileId = await _codaParserService.ImportCodaFileAsync(codaData, organisationId, userId);

            _logger.LogInformation("CODA file {FileName} imported successfully by user {UserId}", viewModel.CodaFile.FileName, userId);

            // Lancer le rapprochement automatique
            var reconciledCount = await _reconciliationService.AutoReconcileTransactionsAsync(codaFileId);

            TempData[TempDataSuccess] = reconciledCount > 0
                ? _localizer["Message.CodaFileImportedWithReconciliation", codaData.Transactions.Count, reconciledCount].Value
                : _localizer["Message.CodaFileImported", codaData.Transactions.Count].Value;

            return RedirectToAction(nameof(CodaFileDetails), new { id = codaFileId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing CODA file");
            ModelState.AddModelError(string.Empty, _localizer["Error.CodaImportFailed", ex.Message].Value);
            return await ImportCoda();
        }
    }

    // GET: Financial/CodaFileDetails/5
    public async Task<IActionResult> CodaFileDetails(int id)
    {
        var codaFile = await _context.CodaFiles
            .Include(cf => cf.Transactions)
            .FirstOrDefaultAsync(cf => cf.Id == id);

        if (codaFile == null)
        {
            return NotFound();
        }

        return View(codaFile);
    }

    // GET: Financial/Reconciliation
    public async Task<IActionResult> Reconciliation()
    {
        var organisationId = _currentUserService.OrganisationId;
        if (!organisationId.HasValue)
        {
            return Unauthorized();
        }

        var viewModel = new ReconciliationViewModel
        {
            UnreconciledTransactions = await _reconciliationService.GetUnreconciledTransactionsAsync(organisationId.Value),
            UnpaidBookings = await _reconciliationService.GetUnpaidBookingsAsync(organisationId.Value),
            Suggestions = await _reconciliationService.GetReconciliationSuggestionsAsync(organisationId.Value)
        };

        return View(viewModel);
    }

    // POST: Financial/ManualReconcile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualReconcile(ManualReconcileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData[TempDataError] = _localizer["Error.InvalidData"].Value;
            return RedirectToAction(nameof(Reconciliation));
        }

        var success = await _reconciliationService.ManualReconcileAsync(model.TransactionId, model.BookingId);

        if (success)
        {
            TempData[TempDataSuccess] = _localizer["Message.TransactionReconciled"].Value;
        }
        else
        {
            TempData[TempDataError] = _localizer["Error.ReconciliationFailed"].Value;
        }

        return RedirectToAction(nameof(Reconciliation));
    }
}
