using Cedeva.Website.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Activities.ViewModels;

/// <summary>
/// Cross-field validation for activities that DataAnnotations can't express: the end date must not
/// precede the start date. (Field-level required/length rules stay as DataAnnotations on the model.)
/// </summary>
public class ActivityViewModelValidator : AbstractValidator<ActivityViewModel>
{
    public ActivityViewModelValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage(_ => localizer["Validation.EndDateBeforeStart"].Value);
    }
}
