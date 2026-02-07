using Cedeva.Infrastructure.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Website.Features.ActivityQuestions.ViewModels;

public class ActivityQuestionViewModelValidator : AbstractValidator<ActivityQuestionViewModel>
{
    private readonly CedevaDbContext _context;

    public ActivityQuestionViewModelValidator(CedevaDbContext context)
    {
        _context = context;

        RuleFor(x => x.DisplayOrder)
            .GreaterThan(0)
            .WithMessage("L'ordre d'affichage doit être supérieur à 0.")
            .MustAsync(BeUniqueDisplayOrderPerActivity)
            .WithMessage("Cet ordre d'affichage est déjà utilisé par une autre question de cette activité.");
    }

    private async Task<bool> BeUniqueDisplayOrderPerActivity(ActivityQuestionViewModel model, int displayOrder, CancellationToken cancellationToken)
    {
        // Check if another question with the same ActivityId and DisplayOrder exists
        // Exclude the current question if editing (Id > 0)
        var exists = await _context.ActivityQuestions
            .AnyAsync(q => q.ActivityId == model.ActivityId
                        && q.DisplayOrder == displayOrder
                        && q.Id != model.Id,
                      cancellationToken);

        return !exists; // Return true if unique (doesn't exist)
    }
}
