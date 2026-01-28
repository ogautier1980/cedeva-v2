using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class Address
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Street { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    public Country Country { get; set; } = Country.Belgium;
}
