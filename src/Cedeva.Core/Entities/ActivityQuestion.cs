using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class ActivityQuestion
{
    public int Id { get; set; }

    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string QuestionText { get; set; } = string.Empty;

    public QuestionType QuestionType { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>
    /// Comma-separated options for dropdown/radio questions (e.g., "Option1,Option2,Option3")
    /// </summary>
    public string? Options { get; set; }

    public ICollection<ActivityQuestionAnswer> Answers { get; set; } = new List<ActivityQuestionAnswer>();
}
