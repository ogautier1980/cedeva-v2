using Cedeva.Core.Interfaces;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.FiscalAttestations;

/// <summary>
/// "Attestations fiscales": every parent of the coordinator's organisation who paid during a year,
/// and the e-mailing of all their attestations (form 281.86) at once.
/// </summary>
[Authorize(Roles = "Admin,Coordinator")]
public class FiscalAttestationsController : Controller
{
    private readonly IFiscalAttestationService _fiscalAttestationService;
    private readonly FiscalAttestationSender _sender;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILogger<FiscalAttestationsController> _logger;

    public FiscalAttestationsController(
        IFiscalAttestationService fiscalAttestationService,
        FiscalAttestationSender sender,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILogger<FiscalAttestationsController> logger)
    {
        _fiscalAttestationService = fiscalAttestationService;
        _sender = sender;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    // Attestations are sent in the year following the expenses: last year by default.
    private static int DefaultYear => DateTime.Today.Year - 1;

    public async Task<IActionResult> Index(int? year)
    {
        var fiscalYear = year ?? DefaultYear;
        var viewModel = new FiscalAttestationsIndexViewModel
        {
            Year = fiscalYear,
            Years = Enumerable.Range(0, 4).Select(offset => DefaultYear + 1 - offset).ToList(),
            HasOrganisation = _currentUserService.OrganisationId.HasValue
        };

        if (_currentUserService.OrganisationId is int organisationId)
        {
            viewModel.Attestations = await _fiscalAttestationService.BuildAllAsync(organisationId, fiscalYear);
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendAll(int year)
    {
        if (_currentUserService.OrganisationId is not int organisationId)
        {
            return RedirectToAction(nameof(Index), new { year });
        }

        var attestations = await _fiscalAttestationService.BuildAllAsync(organisationId, year);
        if (attestations.Count > 0 && attestations[0].MaxDailyAmount == null)
        {
            this.SetErrorMessage(_localizer["FiscalAttestation.YearNotConfigured", year].Value);
            return RedirectToAction(nameof(Index), new { year });
        }

        int sent = 0, withoutEmail = 0, failed = 0;
        foreach (var attestation in attestations)
        {
            try
            {
                if (await _sender.SendAsync(attestation)) sent++;
                else withoutEmail++;
            }
            catch (Exception ex)
            {
                // One failing address must not stop the others.
                failed++;
                _logger.LogError(ex, "Fiscal attestation {Year} could not be sent to parent {ParentId}", year, attestation.ParentId);
            }
        }

        this.SetSuccessMessage(_localizer["FiscalAttestation.SentAll", sent, year].Value);
        if (withoutEmail > 0 || failed > 0)
        {
            this.SetWarningMessage(_localizer["FiscalAttestation.NotSent", withoutEmail + failed].Value);
        }

        return RedirectToAction(nameof(Index), new { year });
    }
}

public class FiscalAttestationsIndexViewModel
{
    public int Year { get; set; }
    public IReadOnlyList<int> Years { get; set; } = [];
    public bool HasOrganisation { get; set; }
    public IReadOnlyList<Core.DTOs.FiscalAttestationData> Attestations { get; set; } = [];
    public bool YearConfigured => Attestations.Count == 0 || Attestations[0].MaxDailyAmount.HasValue;
}
