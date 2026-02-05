using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.ActivityQuestions.ViewModels;

public class ActivityQuestionViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Activity")]
    public int ActivityId { get; set; }

    public string? ActivityName { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "ActivityQuestions.QuestionText")]
    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string QuestionText { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "ActivityQuestions.QuestionType")]
    public QuestionType QuestionType { get; set; }

    [Display(Name = "ActivityQuestions.IsRequired")]
    public bool IsRequired { get; set; }

    [Display(Name = "ActivityQuestions.Options")]
    public string? Options { get; set; }

    public int AnswersCount { get; set; }
}
