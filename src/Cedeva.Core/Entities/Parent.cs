using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Parent
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    [StringLength(100)]
    public string? PhoneNumber { get; set; }

    [Required]
    [StringLength(100)]
    public string MobilePhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(15, MinimumLength = 11)]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public ICollection<Child> Children { get; set; } = new List<Child>();

    public string FullName => $"{LastName}, {FirstName}";
}
