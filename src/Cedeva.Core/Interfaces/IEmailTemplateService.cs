using Cedeva.Core.Entities;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service for managing email templates
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Gets the default template for a specific type and organization
    /// </summary>
    /// <param name="type">Template type</param>
    /// <param name="organisationId">Organization ID</param>
    /// <returns>Default template or null if not found</returns>
    Task<EmailTemplate?> GetDefaultTemplateAsync(EmailTemplateType type, int organisationId);

    /// <summary>
    /// Gets all templates of a specific type for an organization
    /// </summary>
    /// <param name="type">Template type</param>
    /// <param name="organisationId">Organization ID</param>
    /// <returns>List of templates</returns>
    Task<List<EmailTemplate>> GetTemplatesByTypeAsync(EmailTemplateType type, int organisationId);

    /// <summary>
    /// Gets all templates for an organization
    /// </summary>
    /// <param name="organisationId">Organization ID</param>
    /// <returns>List of all templates</returns>
    Task<List<EmailTemplate>> GetAllTemplatesAsync(int organisationId);

    /// <summary>
    /// Gets a template by ID
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <returns>Template or null if not found</returns>
    Task<EmailTemplate?> GetTemplateByIdAsync(int id);

    /// <summary>
    /// Creates a new template
    /// </summary>
    /// <param name="template">Template to create</param>
    /// <returns>Created template with ID</returns>
    Task<EmailTemplate> CreateTemplateAsync(EmailTemplate template);

    /// <summary>
    /// Updates an existing template
    /// </summary>
    /// <param name="template">Template to update</param>
    Task UpdateTemplateAsync(EmailTemplate template);

    /// <summary>
    /// Deletes a template
    /// </summary>
    /// <param name="id">Template ID</param>
    Task DeleteTemplateAsync(int id);

    /// <summary>
    /// Sets a template as the default for its type
    /// Unsets any other default template for the same type and organization
    /// </summary>
    /// <param name="templateId">Template ID to set as default</param>
    /// <param name="type">Template type</param>
    Task SetDefaultTemplateAsync(int templateId, EmailTemplateType type);
}
