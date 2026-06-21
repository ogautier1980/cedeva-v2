using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class BelgianMunicipality
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(10, ErrorMessage = "Validation.StringLength")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string City { get; set; } = string.Empty;
}
