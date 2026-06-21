using Cedeva.Core.Entities;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service for managing email templates. Templates live at two scopes: organisation-level
/// (<c>ActivityId == null</c>, the shared library copied into new activities) and activity-level
/// (<c>ActivityId</c> set, used when emailing for that activity). Each (scope, type) keeps exactly
/// one default.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Gets the default template for a type. When <paramref name="activityId"/> is given, the
    /// activity's default is returned, falling back to the organisation-level default.
    /// </summary>
    Task<EmailTemplate?> GetDefaultTemplateAsync(EmailTemplateType type, int organisationId, int? activityId = null);

    /// <summary>Gets templates of a type in a scope (organisation-level when activityId is null).</summary>
    Task<List<EmailTemplate>> GetTemplatesByTypeAsync(EmailTemplateType type, int organisationId, int? activityId = null);

    /// <summary>Gets all templates in a scope (organisation-level when activityId is null).</summary>
    Task<List<EmailTemplate>> GetAllTemplatesAsync(int organisationId, int? activityId = null);

    /// <summary>Gets a template by ID.</summary>
    Task<EmailTemplate?> GetTemplateByIdAsync(int id);

    /// <summary>
    /// Creates a template. Enforces the one-default-per-(scope, type) rule: the template becomes the
    /// default if it is the first of its type in its scope, and any other default is unset if this
    /// one is marked default.
    /// </summary>
    Task<EmailTemplate> CreateTemplateAsync(EmailTemplate template);

    /// <summary>Updates a template, keeping the one-default-per-(scope, type) rule.</summary>
    Task UpdateTemplateAsync(EmailTemplate template);

    /// <summary>Deletes a template; if it was the default, another template of its type is promoted.</summary>
    Task DeleteTemplateAsync(int id);

    /// <summary>Sets a template as the default for its type within its own scope.</summary>
    Task SetDefaultTemplateAsync(int templateId, EmailTemplateType type);

    /// <summary>
    /// Copies the organisation-level template library into an activity (skips types the activity
    /// already has). Returns the number of templates created.
    /// </summary>
    Task<int> CopyOrganisationTemplatesToActivityAsync(int organisationId, int activityId);

    /// <summary>
    /// Imports templates from one activity into another (same organisation), skipping types the
    /// target already has. Returns the number of templates created.
    /// </summary>
    Task<int> ImportTemplatesFromActivityAsync(int organisationId, int sourceActivityId, int targetActivityId);

    /// <summary>Lists the activities of an organisation that have at least one template (for the import picker).</summary>
    Task<List<ActivityTemplateSummary>> GetActivitiesWithTemplatesAsync(int organisationId, int? excludeActivityId = null);

    /// <summary>
    /// Ensures an organisation has the default template library (org-level). Idempotent. Returns the
    /// number of templates created.
    /// </summary>
    Task<int> EnsureOrganisationLibraryAsync(int organisationId);
}

/// <summary>An activity and how many templates it has (used by the import picker).</summary>
public record ActivityTemplateSummary(int ActivityId, string ActivityName, int TemplateCount);
