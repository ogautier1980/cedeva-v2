using FluentValidation;

namespace Cedeva.Website.Features.ActivityQuestions.ViewModels;

public class ActivityQuestionViewModelValidator : AbstractValidator<ActivityQuestionViewModel>
{
    public ActivityQuestionViewModelValidator()
    {
        // DisplayOrder validation removed - DisplayOrder is managed separately via UpdateOrder method
        // and is not part of the Create/Edit workflow
    }
}
