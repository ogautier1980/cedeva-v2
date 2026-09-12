using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using Cedeva.Core.Interfaces;

namespace Cedeva.Core.Entities;

public class Parent : AuditableEntity, IOrganisationScoped
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation.StringLength")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation.StringLength")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Optional second parent/guardian e-mail — also included as a recipient wherever
    /// the app e-mails the family (custom sends, confirmations, reminders).</summary>
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    [EmailAddress(ErrorMessage = "Validation.InvalidEmail")]
    public string? SecondaryEmail { get; set; }

    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(15, MinimumLength = 11, ErrorMessage = "Validation.StringLength")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public ICollection<Child> Children { get; set; } = new List<Child>();

    public string FullName => $"{LastName}, {FirstName}";

    /// <summary>Both e-mail addresses (primary + optional secondary), non-empty and de-duplicated —
    /// the single place every mail-sending code path should read recipients from.</summary>
    public IEnumerable<string> GetEmailAddresses() =>
        new[] { Email, SecondaryEmail }
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!)
            .Distinct();
}
