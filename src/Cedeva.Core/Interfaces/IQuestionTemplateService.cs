namespace Cedeva.Core.Interfaces;

/// <summary>
/// Seeds a newly created <c>Activity</c> with its organisation's default question templates
/// (Lot J — "modèle de questions par activité"). See <see cref="Entities.OrganisationQuestionTemplate"/>.
/// </summary>
public interface IQuestionTemplateService
{
    /// <summary>Copies every template of <paramref name="organisationId"/> into new
    /// <c>ActivityQuestion</c> rows for <paramref name="activityId"/>. Returns the number copied.</summary>
    Task<int> CopyOrganisationTemplatesToActivityAsync(int organisationId, int activityId);
}
