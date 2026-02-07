using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.ActivityQuestions.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.ActivityQuestions;

[Authorize]
public class ActivityQuestionsController : Controller
{
    private const string TempDataSuccessMessage = "SuccessMessage";
    private const string TempDataErrorMessage = "ErrorMessage";

    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ISessionStateService _sessionState;

    public ActivityQuestionsController(
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IStringLocalizer<SharedResources> localizer,
        ISessionStateService sessionState)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _sessionState = sessionState;
    }

    // GET: ActivityQuestions?activityId=5
    public async Task<IActionResult> Index(int? activityId)
    {
        // Check if any query parameters were provided in the actual HTTP request
        bool hasQueryParams = Request.Query.Count > 0;

        // If query params provided, store them and redirect to clean URL
        if (hasQueryParams)
        {
            if (activityId.HasValue)
                _sessionState.Set<int>("ActivityId", activityId.Value); // ActivityId is context, persists to cookie

            // Redirect to clean URL
            return RedirectToAction(nameof(Index));
        }

        // Load activityId from service (no filters to clear - ActivityId is context, not a filter)
        activityId = _sessionState.Get<int>("ActivityId");

        var query = _context.ActivityQuestions
            .Include(q => q.Activity)
            .Include(q => q.Answers)
            .AsQueryable();

        if (activityId.HasValue)
        {
            query = query.Where(q => q.ActivityId == activityId.Value);
            var activity = await _context.Activities.FindAsync(activityId.Value);
            if (activity != null)
            {
                this.SetActivityViewData(activityId.Value, activity.Name);
            }
        }

        var questions = await query
            .OrderBy(q => q.Activity != null ? q.Activity.Name : "")
            .ThenBy(q => q.DisplayOrder)
            .Select(q => new ActivityQuestionViewModel
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                IsRequired = q.IsRequired,
                Options = q.Options,
                ActivityId = q.ActivityId,
                ActivityName = q.Activity != null ? q.Activity.Name : "",
                AnswersCount = q.Answers.Count,
                DisplayOrder = q.DisplayOrder,
                IsActive = q.IsActive
            })
            .ToListAsync();

        return View(questions);
    }

    // GET: ActivityQuestions/Create?activityId=5
    public async Task<IActionResult> Create(int? activityId)
    {
        await PopulateDropdowns(activityId);

        var model = new ActivityQuestionViewModel();
        if (activityId.HasValue)
        {
            model.ActivityId = activityId.Value;
        }

        return View(model);
    }

    // POST: ActivityQuestions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityQuestionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(model.ActivityId);
            return View(model);
        }

        // Validate that options are provided for Radio and Dropdown question types
        if ((model.QuestionType == QuestionType.Radio || model.QuestionType == QuestionType.Dropdown)
            && string.IsNullOrWhiteSpace(model.Options))
        {
            ModelState.AddModelError(nameof(model.Options), _localizer["ActivityQuestions.OptionsRequired"]);
            await PopulateDropdowns(model.ActivityId);
            return View(model);
        }

        var question = new ActivityQuestion
        {
            QuestionText = model.QuestionText,
            QuestionType = model.QuestionType,
            IsRequired = model.IsRequired,
            Options = model.Options,
            ActivityId = model.ActivityId
        };

        await _context.ActivityQuestions.AddAsync(question);
        await _unitOfWork.SaveChangesAsync();

        TempData[TempDataSuccessMessage] = _localizer["ActivityQuestions.CreateSuccess"].ToString();

        // ActivityId is already in session
        return RedirectToAction(nameof(Index));
    }

    // GET: ActivityQuestions/Edit/5
    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var question = await _context.ActivityQuestions
            .Include(q => q.Activity)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (question == null)
        {
            return NotFound();
        }

        var model = new ActivityQuestionViewModel
        {
            Id = question.Id,
            QuestionText = question.QuestionText,
            QuestionType = question.QuestionType,
            IsRequired = question.IsRequired,
            Options = question.Options,
            ActivityId = question.ActivityId,
            ActivityName = question.Activity?.Name
        };

        await PopulateDropdowns(model.ActivityId);
        ViewData["ReturnUrl"] = returnUrl;

        return View(model);
    }

    // POST: ActivityQuestions/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityQuestionViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(model.ActivityId);
            return View(model);
        }

        // Validate that options are provided for Radio and Dropdown question types
        if ((model.QuestionType == QuestionType.Radio || model.QuestionType == QuestionType.Dropdown)
            && string.IsNullOrWhiteSpace(model.Options))
        {
            ModelState.AddModelError(nameof(model.Options), _localizer["ActivityQuestions.OptionsRequired"]);
            await PopulateDropdowns(model.ActivityId);
            return View(model);
        }

        var question = await _context.ActivityQuestions.FindAsync(id);
        if (question == null)
        {
            return NotFound();
        }

        question.QuestionText = model.QuestionText;
        question.QuestionType = model.QuestionType;
        question.IsRequired = model.IsRequired;
        question.Options = model.Options;
        question.ActivityId = model.ActivityId;

        _context.ActivityQuestions.Update(question);
        await _unitOfWork.SaveChangesAsync();

        TempData[TempDataSuccessMessage] = _localizer["ActivityQuestions.UpdateSuccess"].ToString();

        return this.RedirectToReturnUrlOrAction(returnUrl, nameof(Index));
    }

    // GET: ActivityQuestions/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var question = await _context.ActivityQuestions
            .Include(q => q.Activity)
            .Include(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (question == null)
        {
            return NotFound();
        }

        var model = new ActivityQuestionViewModel
        {
            Id = question.Id,
            QuestionText = question.QuestionText,
            QuestionType = question.QuestionType,
            IsRequired = question.IsRequired,
            Options = question.Options,
            ActivityId = question.ActivityId,
            ActivityName = question.Activity?.Name,
            AnswersCount = question.Answers.Count
        };

        return View(model);
    }

    // POST: ActivityQuestions/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var question = await _context.ActivityQuestions
            .Include(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (question == null)
        {
            return NotFound();
        }

        // Check if question has answers
        if (question.Answers.Any())
        {
            TempData[TempDataErrorMessage] = _localizer["ActivityQuestions.DeleteErrorHasAnswers"].Value;
            return RedirectToAction(nameof(Delete), new { id });
        }

        _context.ActivityQuestions.Remove(question);
        await _unitOfWork.SaveChangesAsync();

        TempData[TempDataSuccessMessage] = _localizer["ActivityQuestions.DeleteSuccess"].ToString();

        // ActivityId is already in session
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns(int? selectedActivityId = null)
    {
        var activities = await _context.Activities
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name })
            .ToListAsync();

        ViewBag.Activities = new SelectList(activities, "Id", "Name", selectedActivityId);

        // Question types dropdown
        var questionTypes = Enum.GetValues(typeof(QuestionType))
            .Cast<QuestionType>()
            .Select(qt => new
            {
                Value = (int)qt,
                Text = _localizer[$"Enum.QuestionType.{qt}"]
            })
            .ToList();

        ViewBag.QuestionTypes = new SelectList(questionTypes, "Value", "Text");
    }

    // POST: ActivityQuestions/UpdateOrder
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrder([FromBody] List<QuestionOrderDto> updates)
    {
        try
        {
            if (updates == null || !updates.Any())
                return Json(new { success = false, message = _localizer["Error.InvalidData"].ToString() });

            // Récupérer toutes les questions à mettre à jour
            var questionIds = updates.Select(u => u.Id).ToList();
            var questions = await _context.ActivityQuestions
                .Where(q => questionIds.Contains(q.Id))
                .ToListAsync();

            if (questions.Count != updates.Count)
                return Json(new { success = false, message = _localizer["Error.NotFound"].ToString() });

            // Vérifier que toutes les questions appartiennent à la même activité (sécurité)
            var activityIds = questions.Select(q => q.ActivityId).Distinct().ToList();
            if (activityIds.Count > 1)
                return Json(new { success = false, message = _localizer["Error.InvalidOperation"].ToString() });

            // Mettre à jour les DisplayOrder
            foreach (var update in updates)
            {
                var question = questions.First(q => q.Id == update.Id);
                question.DisplayOrder = update.DisplayOrder;
            }

            await _unitOfWork.SaveChangesAsync();

            return Json(new { success = true, message = _localizer["Message.OrderUpdated"].ToString() });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = _localizer["Error.UnexpectedError"].ToString() });
        }
    }

    // POST: ActivityQuestions/ToggleActive
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, bool isActive)
    {
        try
        {
            var question = await _context.ActivityQuestions.FindAsync(id);
            if (question == null)
                return Json(new { success = false, message = _localizer["Error.NotFound"].ToString() });

            question.IsActive = isActive;
            await _unitOfWork.SaveChangesAsync();

            var statusKey = isActive ? "Message.QuestionActivated" : "Message.QuestionDeactivated";
            return Json(new { success = true, message = _localizer[statusKey].ToString() });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = _localizer["Error.UnexpectedError"].ToString() });
        }
    }
}
