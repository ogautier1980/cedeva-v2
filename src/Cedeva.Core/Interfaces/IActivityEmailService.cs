namespace Cedeva.Core.Interfaces;

/// <summary>
/// Recipient selector keys shared by the email composer dropdown (view) and the send service.
/// </summary>
public static class EmailRecipientKeys
{
    public const string AllParents = "allparents";
    public const string MedicalSheetReminder = "medicalsheetreminder";
    public const string UnpaidParents = "unpaidparents";
    public const string GroupPrefix = "group_";
    public const string ExcursionPrefix = "excursion_";
    public const string CustomContacts = "custom_contacts";
    public const string ContactGroupPrefix = "contactgroup_";
}

/// <summary>Outcome of an activity email send.</summary>
public enum ActivityEmailOutcome
{
    Sent,
    NoRecipients,
    NoContactsSelected
}

/// <summary>Result of an activity email send (outcome + number of messages sent).</summary>
public record ActivityEmailResult(ActivityEmailOutcome Outcome, int SentCount);

/// <summary>Everything the send service needs from the composed email (no view/HTTP concerns).</summary>
public record ActivityEmailRequest(
    int ActivityId,
    string? SelectedRecipient,
    int? SelectedDayId,
    string Subject,
    string Message,
    bool SendSeparateEmailPerChild,
    IReadOnlyList<string> SelectedContactEmails,
    string? AttachmentFileName,
    string? AttachmentFilePath);

/// <summary>
/// Orchestrates sending a composed email for an activity: resolves recipients (all/medical/unpaid/
/// activity-group/excursion, ad-hoc contacts or a saved contact group), sends per-child or per-parent,
/// and logs the send. View concerns (validation, attachment upload, dropdowns) stay in the controller.
/// </summary>
public interface IActivityEmailService
{
    Task<ActivityEmailResult> SendAsync(ActivityEmailRequest request, CancellationToken ct = default);
}
