using Cedeva.Core.DTOs;

namespace Cedeva.Core.Interfaces;

public interface IBookingQuestionService
{
    /// <summary>
    /// Saves question answers for a booking. Deletes existing answers and creates new ones.
    /// </summary>
    Task SaveAnswersAsync(int bookingId, Dictionary<int, string> answers, CancellationToken ct = default);

    /// <summary>
    /// Gets questions for an activity with optional answers from a booking.
    /// Includes inactive questions if they have answers.
    /// </summary>
    Task<List<ActivityQuestionDto>> GetQuestionsWithAnswersAsync(int activityId, int? bookingId = null, CancellationToken ct = default);

    /// <summary>
    /// Validates that all required questions have non-empty answers.
    /// </summary>
    Task<bool> ValidateRequiredQuestionsAsync(int activityId, Dictionary<int, string> answers, CancellationToken ct = default);
}
