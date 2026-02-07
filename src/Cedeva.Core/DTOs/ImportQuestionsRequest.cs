namespace Cedeva.Core.DTOs;

/// <summary>
/// Request DTO for importing questions from another activity
/// </summary>
public class ImportQuestionsRequest
{
    public List<int> QuestionIds { get; set; } = new();
}
