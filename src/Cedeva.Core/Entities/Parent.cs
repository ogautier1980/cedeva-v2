using System.ComponentModel.DataAnnotations;

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
}
