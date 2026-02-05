using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Parent
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(15, MinimumLength = 11, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public ICollection<Child> Children { get; set; } = new List<Child>();

    public string FullName => $"{LastName}, {FirstName}";
}
