using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Email;

/// <summary>
/// Service for managing email templates. Templates are scoped either to an organisation
/// (<c>ActivityId == null</c>, the library) or to an activity (<c>ActivityId</c> set). Each
/// (scope, type) keeps exactly one default.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly CedevaDbContext _context;

    public EmailTemplateService(CedevaDbContext context)
    {
        _context = context;
    }

    public async Task<EmailTemplate?> GetDefaultTemplateAsync(EmailTemplateType type, int organisationId, int? activityId = null)
    {
        if (activityId.HasValue)
        {
            var activityDefault = await _context.EmailTemplates
                .Where(t => t.OrganisationId == organisationId && t.ActivityId == activityId
                            && t.TemplateType == type && t.IsDefault)
                .FirstOrDefaultAsync();
            if (activityDefault != null)
                return activityDefault;
        }

        return await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId && t.ActivityId == null
                        && t.TemplateType == type && t.IsDefault)
            .FirstOrDefaultAsync();
    }

    public async Task<List<EmailTemplate>> GetTemplatesByTypeAsync(EmailTemplateType type, int organisationId, int? activityId = null)
    {
        return await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId && t.ActivityId == activityId && t.TemplateType == type)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<EmailTemplate>> GetAllTemplatesAsync(int organisationId, int? activityId = null)
    {
        return await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId && t.ActivityId == activityId)
            .OrderBy(t => t.TemplateType)
            .ThenByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<EmailTemplate?> GetTemplateByIdAsync(int id)
    {
        return await _context.EmailTemplates
            .Include(t => t.Organisation)
            .Include(t => t.CreatedByUser)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<EmailTemplate> CreateTemplateAsync(EmailTemplate template)
    {
        template.CreatedAt = DateTime.UtcNow;

        // The first template of a type in its scope must be the default.
        var hasDefault = await ScopeHasDefaultAsync(template.TemplateType, template.OrganisationId, template.ActivityId);
        if (!hasDefault)
            template.IsDefault = true;

        if (template.IsDefault)
            await UnsetOtherDefaultsAsync(template.TemplateType, template.OrganisationId, template.ActivityId);

        _context.EmailTemplates.Add(template);
        await _context.SaveChangesAsync();

        return template;
    }

    public async Task UpdateTemplateAsync(EmailTemplate template)
    {
        template.ModifiedAt = DateTime.UtcNow;

        if (template.IsDefault)
        {
            await UnsetOtherDefaultsAsync(template.TemplateType, template.OrganisationId, template.ActivityId, template.Id);
        }
        else
        {
            // Don't allow unsetting the only default of a scope; keep it default.
            var hasOtherDefault = await _context.EmailTemplates.AnyAsync(t =>
                t.OrganisationId == template.OrganisationId && t.ActivityId == template.ActivityId
                && t.TemplateType == template.TemplateType && t.IsDefault && t.Id != template.Id);
            if (!hasOtherDefault)
                template.IsDefault = true;
        }

        _context.EmailTemplates.Update(template);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteTemplateAsync(int id)
    {
        var template = await _context.EmailTemplates.FindAsync(id);
        if (template == null)
            return;

        var wasDefault = template.IsDefault;
        _context.EmailTemplates.Remove(template);
        await _context.SaveChangesAsync();

        // Promote another template of the same type/scope to default if the deleted one was it.
        if (wasDefault)
        {
            var replacement = await _context.EmailTemplates
                .Where(t => t.OrganisationId == template.OrganisationId && t.ActivityId == template.ActivityId
                            && t.TemplateType == template.TemplateType)
                .OrderBy(t => t.Name)
                .FirstOrDefaultAsync();
            if (replacement != null)
            {
                replacement.IsDefault = true;
                replacement.ModifiedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task SetDefaultTemplateAsync(int templateId, EmailTemplateType type)
    {
        var template = await _context.EmailTemplates.FindAsync(templateId);
        if (template == null)
            throw new InvalidOperationException($"Template with ID {templateId} not found");

        await UnsetOtherDefaultsAsync(type, template.OrganisationId, template.ActivityId, templateId);

        template.IsDefault = true;
        template.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<int> CopyOrganisationTemplatesToActivityAsync(int organisationId, int activityId)
    {
        var orgTemplates = await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId && t.ActivityId == null)
            .ToListAsync();

        var existingTypes = await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId && t.ActivityId == activityId)
            .Select(t => t.TemplateType)
            .ToListAsync();

        return await CopyTemplatesAsync(orgTemplates, existingTypes, organisationId, activityId);
    }

    public async Task<int> ImportTemplatesFromActivityAsync(int organisationId, int sourceActivityId, int targetActivityId)
    {
        var sourceTemplates = await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId && t.ActivityId == sourceActivityId)
            .ToListAsync();

        var existingTypes = await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId && t.ActivityId == targetActivityId)
            .Select(t => t.TemplateType)
            .ToListAsync();

        return await CopyTemplatesAsync(sourceTemplates, existingTypes, organisationId, targetActivityId);
    }

    public async Task<List<ActivityTemplateSummary>> GetActivitiesWithTemplatesAsync(int organisationId, int? excludeActivityId = null)
    {
        var rows = await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId && t.ActivityId != null
                        && (excludeActivityId == null || t.ActivityId != excludeActivityId))
            .Select(t => new { ActivityId = t.ActivityId!.Value, ActivityName = t.Activity!.Name })
            .ToListAsync();

        return rows
            .GroupBy(r => new { r.ActivityId, r.ActivityName })
            .Select(g => new ActivityTemplateSummary(g.Key.ActivityId, g.Key.ActivityName, g.Count()))
            .OrderBy(s => s.ActivityName)
            .ToList();
    }

    private async Task<int> CopyTemplatesAsync(
        List<EmailTemplate> source, List<EmailTemplateType> existingTypes, int organisationId, int targetActivityId)
    {
        var now = DateTime.UtcNow;
        var created = 0;
        foreach (var t in source)
        {
            // Skip if the target already has a template of this type (don't clobber existing work).
            if (existingTypes.Contains(t.TemplateType))
                continue;

            _context.EmailTemplates.Add(new EmailTemplate
            {
                OrganisationId = organisationId,
                ActivityId = targetActivityId,
                Name = t.Name,
                TemplateType = t.TemplateType,
                Subject = t.Subject,
                HtmlContent = t.HtmlContent,
                IsDefault = t.IsDefault,
                CreatedAt = now
            });
            existingTypes.Add(t.TemplateType); // keep one default per type even if source had several
            created++;
        }

        if (created > 0)
            await _context.SaveChangesAsync();
        return created;
    }

    private async Task<bool> ScopeHasDefaultAsync(EmailTemplateType type, int organisationId, int? activityId) =>
        await _context.EmailTemplates.AnyAsync(t =>
            t.OrganisationId == organisationId && t.ActivityId == activityId
            && t.TemplateType == type && t.IsDefault);

    /// <summary>Unsets IsDefault for other templates of the same type within the same scope.</summary>
    private async Task UnsetOtherDefaultsAsync(EmailTemplateType type, int organisationId, int? activityId, int? excludeTemplateId = null)
    {
        var query = _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId && t.ActivityId == activityId
                        && t.TemplateType == type && t.IsDefault);

        if (excludeTemplateId.HasValue)
            query = query.Where(t => t.Id != excludeTemplateId.Value);

        foreach (var existingDefault in await query.ToListAsync())
        {
            existingDefault.IsDefault = false;
            existingDefault.ModifiedAt = DateTime.UtcNow;
        }
    }
}
