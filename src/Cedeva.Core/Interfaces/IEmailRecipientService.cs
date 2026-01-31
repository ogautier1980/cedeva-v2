namespace Cedeva.Core.Interfaces;

public interface IEmailRecipientService
{
    /// <summary>
    /// Gets recipient email addresses based on activity and selection criteria
    /// </summary>
    /// <param name="activityId">Activity ID</param>
    /// <param name="selectedRecipient">Selected recipient criteria (allparents, medicalsheetreminder, or group_X)</param>
    /// <param name="recipientGroupId">Optional group ID if recipient type is group</param>
    /// <param name="scheduledDayId">Optional activity day ID to filter recipients by specific day</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of email addresses</returns>
    Task<List<string>> GetRecipientEmailsAsync(
        int activityId,
        string selectedRecipient,
        int? recipientGroupId = null,
        int? scheduledDayId = null,
        CancellationToken cancellationToken = default);
}
