using Cedeva.Core.Entities;
using Cedeva.Core.Helpers;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Activities.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Activities;

/// <summary>
/// Assistant de création d'activité en 7 étapes (Lot I). Étape 1 crée l'<see cref="Activity"/> ;
/// les étapes suivantes chargent puis modifient un sous-ensemble de champs sur cette même entité,
/// à la manière de <see cref="ActivitiesController.Edit(int, string?)"/>, chacune redirigeant vers
/// l'étape suivante (PRG). L'étape 7 renvoie vers l'écran de personnalisation iframe existant
/// (<c>Activities/Details</c>), inchangé.
/// </summary>
[Authorize]
public class ActivityWizardController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ActivityWizardController> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IEmailTemplateService _templateService;
    private readonly IActivityDayService _activityDayService;

    public ActivityWizardController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ActivityWizardController> logger,
        IStringLocalizer<SharedResources> localizer,
        IEmailTemplateService templateService,
        IActivityDayService activityDayService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _localizer = localizer;
        _templateService = templateService;
        _activityDayService = activityDayService;
    }

    // ------------------------------------------------------------------
    // Step 1 — Titre + Dates (crée l'activité)
    // ------------------------------------------------------------------

    [HttpGet]
    public IActionResult Step1()
    {
        var viewModel = new WizardStep1ViewModel
        {
            OrganisationId = _currentUserService.OrganisationId ?? 0
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step1(WizardStep1ViewModel viewModel)
    {
        if (viewModel.EndDate < viewModel.StartDate)
        {
            ModelState.AddModelError(nameof(viewModel.EndDate), _localizer["Validation.EndDateAfterStartDate"]);
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var organisationId = _currentUserService.OrganisationId;
        if (!_currentUserService.IsAdmin && organisationId == null)
        {
            return Forbid();
        }

        var activity = new Activity
        {
            Name = viewModel.Name,
            Description = string.Empty,
            IsActive = true,
            StartDate = viewModel.StartDate,
            EndDate = viewModel.EndDate,
            OrganisationId = _currentUserService.IsAdmin ? viewModel.OrganisationId : organisationId!.Value
        };

        ActivityDayGenerator.GenerateDays(activity);

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        await _templateService.CopyOrganisationTemplatesToActivityAsync(activity.OrganisationId, activity.Id);

        _logger.LogInformation("Activity {Name} created via wizard by user {UserId}", activity.Name, _currentUserService.UserId);

        return RedirectToAction(nameof(Step2), new { id = activity.Id });
    }

    // ------------------------------------------------------------------
    // Step 2 — Paramétrage des dates
    // ------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Step2(int id)
    {
        var activity = await LoadActivityWithDaysAsync(id);
        if (activity == null) return NotFound();

        return View(BuildStep2ViewModel(activity));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDate(int id, DateTime date)
    {
        var activity = await LoadActivityWithDaysAsync(id);
        if (activity == null) return NotFound();

        var existingDay = activity.Days.FirstOrDefault(d => d.DayDate.Date == date.Date);
        if (existingDay != null)
        {
            existingDay.IsActive = true;
        }
        else if (date < activity.StartDate)
        {
            var oldStart = activity.StartDate;
            activity.StartDate = date;
            ActivityDayGenerator.HandleDateRangeChanges(activity, activity.StartDate, activity.EndDate, oldStart, activity.EndDate);
        }
        else if (date > activity.EndDate)
        {
            var oldEnd = activity.EndDate;
            activity.EndDate = date;
            ActivityDayGenerator.HandleDateRangeChanges(activity, activity.StartDate, activity.EndDate, activity.StartDate, oldEnd);
        }

        await _activityDayService.ReconcileTeamMemberDaysAsync(activity);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Step2), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step2Next(int id, List<int>? ActiveDayIds)
    {
        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.Bookings)
                .ThenInclude(b => b.Days)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null) return NotFound();

        if (ActiveDayIds != null)
        {
            var dayResult = await _activityDayService.ApplyDayActivationChangesAsync(
                activity, ActiveDayIds, addDaysToBookings: false, removeDaysConfirmed: true);

            if (dayResult.Outcome != DayActivationOutcome.Applied)
            {
                // No bookings can exist yet at this point in the wizard (the activity was just
                // created), so the confirmation/info branches of Edit's day editor are unreachable
                // here — but guard anyway rather than silently drop the change.
                return RedirectToAction(nameof(Step2), new { id });
            }
        }

        await _activityDayService.ReconcileTeamMemberDaysAsync(activity);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Step3), new { id });
    }

    private static WizardStep2ViewModel BuildStep2ViewModel(Activity activity) => new()
    {
        ActivityId = activity.Id,
        ActivityName = activity.Name,
        Weeks = activity.Days
            .OrderBy(d => d.DayDate)
            .GroupBy(d => d.Week ?? 0)
            .OrderBy(g => g.Key)
            .Select(g => new WizardDayWeekViewModel
            {
                WeekNumber = g.Key,
                Days = g.Select(d => new WizardDayViewModel
                {
                    DayId = d.DayId,
                    Label = d.Label,
                    Date = d.DayDate,
                    IsActive = d.IsActive,
                    IsWeekend = ActivityDayGenerator.IsWeekend(d.DayDate)
                }).ToList()
            }).ToList()
    };

    // ------------------------------------------------------------------
    // Step 3 — Règlement
    // ------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Step3(int id)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null) return NotFound();

        return View(new WizardStep3ViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            RegulationLinkUrl = activity.RegulationLinkUrl,
            RegulationAcceptanceText = activity.RegulationAcceptanceText
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step3(WizardStep3ViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == viewModel.ActivityId);
        if (activity == null) return NotFound();

        activity.RegulationLinkUrl = string.IsNullOrWhiteSpace(viewModel.RegulationLinkUrl) ? null : viewModel.RegulationLinkUrl.Trim();
        activity.RegulationAcceptanceText = string.IsNullOrWhiteSpace(viewModel.RegulationAcceptanceText) ? null : viewModel.RegulationAcceptanceText.Trim();
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Step4), new { id = activity.Id });
    }

    // ------------------------------------------------------------------
    // Step 4 — Limitations
    // ------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Step4(int id)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null) return NotFound();

        return View(new WizardStep4ViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            IncludedPostalCodes = activity.IncludedPostalCodes,
            ExcludedPostalCodes = activity.ExcludedPostalCodes,
            PostalCodeErrorMessage = activity.PostalCodeErrorMessage,
            MaxChildrenPerDay = activity.MaxChildrenPerDay,
            FullMessage = activity.FullMessage
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step4(WizardStep4ViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == viewModel.ActivityId);
        if (activity == null) return NotFound();

        activity.IncludedPostalCodes = string.IsNullOrWhiteSpace(viewModel.IncludedPostalCodes) ? null : viewModel.IncludedPostalCodes.Trim();
        activity.ExcludedPostalCodes = string.IsNullOrWhiteSpace(viewModel.ExcludedPostalCodes) ? null : viewModel.ExcludedPostalCodes.Trim();
        activity.PostalCodeErrorMessage = string.IsNullOrWhiteSpace(viewModel.PostalCodeErrorMessage) ? null : viewModel.PostalCodeErrorMessage.Trim();
        activity.MaxChildrenPerDay = viewModel.MaxChildrenPerDay;
        activity.FullMessage = string.IsNullOrWhiteSpace(viewModel.FullMessage) ? null : viewModel.FullMessage.Trim();
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Step5), new { id = activity.Id });
    }

    // ------------------------------------------------------------------
    // Step 5 — Autres questions
    // ------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Step5(int id)
    {
        var activity = await _context.Activities
            .Include(a => a.AdditionalQuestions)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null) return NotFound();

        return View(BuildStep5ViewModel(activity));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step5(WizardStep5ViewModel viewModel)
    {
        var activity = await _context.Activities
            .Include(a => a.AdditionalQuestions)
            .FirstOrDefaultAsync(a => a.Id == viewModel.ActivityId);
        if (activity == null) return NotFound();

        if (!ModelState.IsValid)
        {
            viewModel.ActivityName = activity.Name;
            return View(viewModel);
        }

        UpdateExistingQuestions(viewModel, activity.Id);
        AddNewQuestions(viewModel, activity.Id);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Step6), new { id = activity.Id });
    }

    private static WizardStep5ViewModel BuildStep5ViewModel(Activity activity) => new()
    {
        ActivityId = activity.Id,
        ActivityName = activity.Name,
        ExistingQuestions = activity.AdditionalQuestions
            .OrderBy(q => q.DisplayOrder)
            .Select(q => new ExistingActivityQuestionViewModel
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                IsRequired = q.IsRequired,
                Options = q.Options,
                DisplayOrder = q.DisplayOrder,
                IsActive = q.IsActive
            }).ToList()
    };

    private void UpdateExistingQuestions(WizardStep5ViewModel viewModel, int activityId)
    {
        if (viewModel.ExistingQuestions.Count == 0) return;

        var existingQuestions = _context.ActivityQuestions.Where(q => q.ActivityId == activityId).ToList();
        foreach (var questionVm in viewModel.ExistingQuestions)
        {
            var question = existingQuestions.FirstOrDefault(q => q.Id == questionVm.Id);
            if (question == null) continue;
            question.QuestionText = questionVm.QuestionText.Trim();
            question.QuestionType = questionVm.QuestionType;
            question.IsRequired = questionVm.IsRequired;
            question.Options = questionVm.Options?.Trim();
            question.DisplayOrder = questionVm.DisplayOrder;
            // IsActive intentionally untouched here — the wizard no longer exposes this toggle
            // (a question asked for the activity is active by default); the hidden field on
            // Step5.cshtml just round-trips the existing value.
            question.IsActive = questionVm.IsActive;
        }
    }

    private void AddNewQuestions(WizardStep5ViewModel viewModel, int activityId)
    {
        if (viewModel.NewQuestions.Count == 0) return;

        var maxDisplayOrder = _context.ActivityQuestions
            .Where(q => q.ActivityId == activityId)
            .Select(q => (int?)q.DisplayOrder)
            .Max() ?? 0;

        foreach (var questionVm in viewModel.NewQuestions.Where(q => !string.IsNullOrWhiteSpace(q.QuestionText)))
        {
            maxDisplayOrder++;
            _context.ActivityQuestions.Add(new ActivityQuestion
            {
                ActivityId = activityId,
                QuestionText = questionVm.QuestionText.Trim(),
                QuestionType = questionVm.QuestionType,
                IsRequired = questionVm.IsRequired,
                Options = questionVm.Options?.Trim(),
                DisplayOrder = maxDisplayOrder,
                IsActive = true
            });
        }
    }

    // ------------------------------------------------------------------
    // Step 6 — Affichage
    // ------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Step6(int id)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null) return NotFound();

        return View(new WizardStep6ViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            PublicationStartDate = activity.PublicationStartDate,
            PublicationEndDate = activity.PublicationEndDate,
            NoActiveFormMessage = activity.NoActiveFormMessage,
            RedirectUrlAfterSubmit = activity.RedirectUrlAfterSubmit
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Step6(WizardStep6ViewModel viewModel)
    {
        if (viewModel.PublicationStartDate.HasValue && viewModel.PublicationEndDate.HasValue
            && viewModel.PublicationEndDate < viewModel.PublicationStartDate)
        {
            ModelState.AddModelError(nameof(viewModel.PublicationEndDate), _localizer["Validation.EndDateAfterStartDate"]);
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == viewModel.ActivityId);
        if (activity == null) return NotFound();

        activity.PublicationStartDate = viewModel.PublicationStartDate;
        activity.PublicationEndDate = viewModel.PublicationEndDate;
        activity.NoActiveFormMessage = string.IsNullOrWhiteSpace(viewModel.NoActiveFormMessage) ? null : viewModel.NoActiveFormMessage.Trim();
        activity.RedirectUrlAfterSubmit = string.IsNullOrWhiteSpace(viewModel.RedirectUrlAfterSubmit) ? null : viewModel.RedirectUrlAfterSubmit.Trim();
        await _context.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ActivityCreated"].Value;

        // Step 7 = the existing iframe personalization screen, unchanged.
        return RedirectToAction("EmbedCode", "PublicRegistration", new { id = activity.Id });
    }

    // ------------------------------------------------------------------

    private async Task<Activity?> LoadActivityWithDaysAsync(int id) =>
        await _context.Activities.Include(a => a.Days).FirstOrDefaultAsync(a => a.Id == id);
}
