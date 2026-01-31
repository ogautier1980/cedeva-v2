using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services;

/// <summary>
/// Service for managing email templates
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly CedevaDbContext _context;

    public EmailTemplateService(CedevaDbContext context)
    {
        _context = context;
    }

    public async Task<EmailTemplate?> GetDefaultTemplateAsync(EmailTemplateType type, int organisationId)
    {
        return await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId
                        && t.TemplateType == type
                        && t.IsDefault)
            .FirstOrDefaultAsync();
    }

    public async Task<List<EmailTemplate>> GetTemplatesByTypeAsync(EmailTemplateType type, int organisationId)
    {
        return await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId
                        && t.TemplateType == type)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<EmailTemplate>> GetAllTemplatesAsync(int organisationId)
    {
        return await _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId)
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
        template.CreatedDate = DateTime.UtcNow;

        // If this template is marked as default, unset other defaults
        if (template.IsDefault)
        {
            await UnsetOtherDefaultsAsync(template.TemplateType, template.OrganisationId);
        }

        _context.EmailTemplates.Add(template);
        await _context.SaveChangesAsync();

        return template;
    }

    public async Task UpdateTemplateAsync(EmailTemplate template)
    {
        template.LastModifiedDate = DateTime.UtcNow;

        // If this template is marked as default, unset other defaults
        if (template.IsDefault)
        {
            await UnsetOtherDefaultsAsync(template.TemplateType, template.OrganisationId, template.Id);
        }

        _context.EmailTemplates.Update(template);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteTemplateAsync(int id)
    {
        var template = await _context.EmailTemplates.FindAsync(id);
        if (template != null)
        {
            _context.EmailTemplates.Remove(template);
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetDefaultTemplateAsync(int templateId, EmailTemplateType type)
    {
        var template = await _context.EmailTemplates.FindAsync(templateId);
        if (template == null)
            throw new InvalidOperationException($"Template with ID {templateId} not found");

        // Unset other defaults for this type and organization
        await UnsetOtherDefaultsAsync(type, template.OrganisationId, templateId);

        // Set this template as default
        template.IsDefault = true;
        template.LastModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Unsets IsDefault for all other templates of the same type and organization
    /// </summary>
    private async Task UnsetOtherDefaultsAsync(EmailTemplateType type, int organisationId, int? excludeTemplateId = null)
    {
        var query = _context.EmailTemplates
            .Where(t => t.OrganisationId == organisationId
                        && t.TemplateType == type
                        && t.IsDefault);

        if (excludeTemplateId.HasValue)
        {
            query = query.Where(t => t.Id != excludeTemplateId.Value);
        }

        var existingDefaults = await query.ToListAsync();

        foreach (var existingDefault in existingDefaults)
        {
            existingDefault.IsDefault = false;
            existingDefault.LastModifiedDate = DateTime.UtcNow;
        }
    }
}
