using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class BelgianMunicipality
{
    public int Id { get; set; }

    [Required]
    [StringLength(10)]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string City { get; set; } = string.Empty;
}
