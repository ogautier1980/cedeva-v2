using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.QuestionTemplates.ViewModels;

/// <summary>Create/Edit form for an organisation-level default question template (Lot J).</summary>
public class QuestionTemplateViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "ActivityQuestions.QuestionText")]
    public string QuestionText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "ActivityQuestions.QuestionType")]
    public QuestionType QuestionType { get; set; }

    [Display(Name = "ActivityQuestions.IsRequired")]
    public bool IsRequired { get; set; }

    [StringLength(1000, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "ActivityQuestions.Options")]
    public string? Options { get; set; }

    [Display(Name = "QuestionTemplate.DisplayOrder")]
    public int DisplayOrder { get; set; } = 1;
}
