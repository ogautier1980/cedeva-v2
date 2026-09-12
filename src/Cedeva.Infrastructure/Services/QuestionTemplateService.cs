using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services;

public class QuestionTemplateService : IQuestionTemplateService
{
    private readonly CedevaDbContext _context;

    public QuestionTemplateService(CedevaDbContext context)
    {
        _context = context;
    }

    public async Task<int> CopyOrganisationTemplatesToActivityAsync(int organisationId, int activityId)
    {
        var templates = await _context.OrganisationQuestionTemplates
            .Where(t => t.OrganisationId == organisationId)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();

        if (templates.Count == 0) return 0;

        foreach (var template in templates)
        {
            _context.ActivityQuestions.Add(new ActivityQuestion
            {
                ActivityId = activityId,
                QuestionText = template.QuestionText,
                QuestionType = template.QuestionType,
                IsRequired = template.IsRequired,
                Options = template.Options,
                DisplayOrder = template.DisplayOrder,
                IsActive = true
            });
        }

        await _context.SaveChangesAsync();
        return templates.Count;
    }
}
