using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.ActivityQuestions.ViewModels;

public class ActivityQuestionViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Field.Activity")]
    public int ActivityId { get; set; }

    public string? ActivityName { get; set; }

    [Required]
    [Display(Name = "ActivityQuestions.QuestionText")]
    [StringLength(500)]
    public string QuestionText { get; set; } = string.Empty;

    [Required]
    [Display(Name = "ActivityQuestions.QuestionType")]
    public QuestionType QuestionType { get; set; }

    [Display(Name = "ActivityQuestions.IsRequired")]
    public bool IsRequired { get; set; }

    [Display(Name = "ActivityQuestions.Options")]
    public string? Options { get; set; }

    public int AnswersCount { get; set; }
}
