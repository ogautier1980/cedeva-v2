using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class Address : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation.StringLength")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation.StringLength")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(10, ErrorMessage = "Validation.StringLength")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    public Country Country { get; set; } = Country.Belgium;
}
