namespace Cedeva.Core.Interfaces;

/// <summary>Which part of the directory a selectable contact comes from.</summary>
public enum ContactSource
{
    Parent,
    TeamMember,
    Other
}

/// <summary>A contact (with an email) that can be picked as an email recipient.</summary>
public record SelectableContact(string Email, string Display, ContactSource Source);

/// <summary>
/// Builds the list of an organisation's selectable email contacts (parents, team members and
/// "other contacts"), de-duplicated by email. Shared by the email composer and contact-group editor.
/// </summary>
public interface IContactDirectoryService
{
    Task<List<SelectableContact>> GetSelectableContactsAsync(int organisationId, CancellationToken ct = default);
}
