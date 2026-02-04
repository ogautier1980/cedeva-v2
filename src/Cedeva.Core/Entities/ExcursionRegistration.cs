using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// Inscription d'un enfant (via son booking) à une excursion
/// </summary>
public class ExcursionRegistration
{
    public int Id { get; set; }

    [Required]
    public int ExcursionId { get; set; }
    public Excursion Excursion { get; set; } = null!;

    [Required]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    [Required]
    public DateTime RegistrationDate { get; set; }

    /// <summary>
    /// Présence à l'excursion
    /// </summary>
    [Required]
    public bool IsPresent { get; set; } = false;

    /// <summary>
    /// Notes spécifiques (besoins particuliers, etc.)
    /// </summary>
    public string? Notes { get; set; }
}
