using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class Address
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(10, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    public Country Country { get; set; } = Country.Belgium;
}
