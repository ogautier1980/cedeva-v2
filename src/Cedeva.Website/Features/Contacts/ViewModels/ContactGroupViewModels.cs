using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Interfaces;

namespace Cedeva.Website.Features.Contacts.ViewModels;

/// <summary>One row in the saved contact-groups list.</summary>
public class ContactGroupRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public List<string> MemberNames { get; set; } = new();
}

/// <summary>The saved contact-groups index.</summary>
public class ContactGroupListViewModel
{
    public List<ContactGroupRowViewModel> Groups { get; set; } = new();
}

/// <summary>A selectable contact in the group editor.</summary>
public class ContactPickItem
{
    public string Email { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public ContactSource Source { get; set; }
}

/// <summary>Create/Edit form for a saved contact group.</summary>
public class ContactGroupFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Emails of the contacts currently in the group.</summary>
    public List<string> SelectedEmails { get; set; } = new();

    /// <summary>All contacts (with an email) that can be added to the group.</summary>
    public List<ContactPickItem> AvailableContacts { get; set; } = new();
}
