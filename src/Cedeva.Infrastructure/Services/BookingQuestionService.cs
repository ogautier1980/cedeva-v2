using Cedeva.Core.DTOs;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services;

public class BookingQuestionService : IBookingQuestionService
{
    private readonly CedevaDbContext _context;

    public BookingQuestionService(CedevaDbContext context)
    {
        _context = context;
    }

    public async Task SaveAnswersAsync(int bookingId, Dictionary<int, string> answers, CancellationToken ct = default)
    {
        // Delete existing answers
        var existingAnswers = await _context.ActivityQuestionAnswers
            .Where(a => a.BookingId == bookingId)
            .ToListAsync(ct);

        _context.ActivityQuestionAnswers.RemoveRange(existingAnswers);

        // Add new answers (only non-empty values)
        if (answers != null)
        {
            foreach (var answer in answers.Where(a => !string.IsNullOrWhiteSpace(a.Value)))
            {
                var questionAnswer = new ActivityQuestionAnswer
                {
                    BookingId = bookingId,
                    ActivityQuestionId = answer.Key,
                    AnswerText = answer.Value
                };
                _context.ActivityQuestionAnswers.Add(questionAnswer);
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ActivityQuestionDto>> GetQuestionsWithAnswersAsync(int activityId, int? bookingId = null, CancellationToken ct = default)
    {
        // Load existing answers if bookingId provided
        List<ActivityQuestionAnswer> existingAnswers = new();
        if (bookingId.HasValue)
        {
            existingAnswers = await _context.ActivityQuestionAnswers
                .Where(a => a.BookingId == bookingId.Value)
                .ToListAsync(ct);
        }

        // Load questions: active ones OR ones that have answers (even if inactive)
        var allQuestions = await _context.ActivityQuestions
            .Where(q => q.ActivityId == activityId)
            .Where(q => q.IsActive || existingAnswers.Any(a => a.ActivityQuestionId == q.Id))
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync(ct);

        // Map to DTOs
        var questions = allQuestions.Select(q => new ActivityQuestionDto
        {
            Id = q.Id,
            QuestionText = q.QuestionText,
            QuestionType = q.QuestionType,
            IsRequired = q.IsRequired,
            Options = q.Options,
            DisplayOrder = q.DisplayOrder,
            AnswerText = existingAnswers.FirstOrDefault(a => a.ActivityQuestionId == q.Id)?.AnswerText
        }).ToList();

        return questions;
    }

    public async Task<bool> ValidateRequiredQuestionsAsync(int activityId, Dictionary<int, string> answers, CancellationToken ct = default)
    {
        // Get all required questions for the activity
        var requiredQuestionIds = await _context.ActivityQuestions
            .Where(q => q.ActivityId == activityId && q.IsActive && q.IsRequired)
            .Select(q => q.Id)
            .ToListAsync(ct);

        // Check all required questions have non-empty answers
        foreach (var questionId in requiredQuestionIds)
        {
            if (!answers.TryGetValue(questionId, out var answer) || string.IsNullOrWhiteSpace(answer))
            {
                return false;
            }
        }

        return true;
    }
}
