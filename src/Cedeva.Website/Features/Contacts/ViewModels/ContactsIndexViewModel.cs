namespace Cedeva.Website.Features.Contacts.ViewModels;

/// <summary>One row in the contacts list (any category).</summary>
public class ContactRowViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Function { get; set; }

    /// <summary>Set only for "Autres contacts" rows, so they can be edited/deleted.</summary>
    public int? ContactId { get; set; }
}

/// <summary>All of an organisation's contacts, grouped by function.</summary>
public class ContactsIndexViewModel
{
    public List<ContactRowViewModel> Admins { get; set; } = new();
    public List<ContactRowViewModel> Coordinators { get; set; } = new();
    public List<ContactRowViewModel> TeamMembers { get; set; } = new();
    public List<ContactRowViewModel> Parents { get; set; } = new();
    public List<ContactRowViewModel> Others { get; set; } = new();

    public int TotalCount => Admins.Count + Coordinators.Count + TeamMembers.Count + Parents.Count + Others.Count;
}

/// <summary>One rendered category section (used by the _ContactCategory partial).</summary>
public class ContactCategoryViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<ContactRowViewModel> Rows { get; set; } = new();
    public bool Editable { get; set; }
}
