using Cedeva.Core.Enums;

namespace Cedeva.Core.DTOs;

public class ActivityQuestionDto
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public bool IsRequired { get; set; }
    public string? Options { get; set; }
    public int DisplayOrder { get; set; }
    public string? AnswerText { get; set; }
}
