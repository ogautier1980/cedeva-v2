using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// Inscription d'un enfant (via son booking) à une excursion
/// </summary>
public class ExcursionRegistration : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int ExcursionId { get; set; }
    public Excursion Excursion { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    public DateTime RegistrationDate { get; set; }

    /// <summary>
    /// Présence à l'excursion
    /// </summary>
    [Required(ErrorMessage = "Validation.Required")]
    public bool IsPresent { get; set; } = false;

    /// <summary>
    /// Notes spécifiques (besoins particuliers, etc.)
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>Montant déjà payé pour cette excursion (le "prévu" reste <see cref="Excursion.Cost"/>,
    /// commun à tous les enfants inscrits — seul le payé est propre à chaque inscription).</summary>
    [Range(0, 9999999, ErrorMessage = "Validation.AmountRange")]
    public decimal PaidAmount { get; set; }
}
