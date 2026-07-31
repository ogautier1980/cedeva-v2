using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

/// <summary>
/// Paiement effectué pour une réservation (virement bancaire ou cash).
/// </summary>
public class Payment : AuditableEntity
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    /// <summary>
    /// Numéro de ticket, unique par activité (remis à 1 à chaque nouvelle activité — partage la
    /// même séquence que <see cref="Expense.TicketNumber"/> pour cette activité).
    /// </summary>
    public int TicketNumber { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Communication structurée belge (format +++XXX/XXXX/XXXXX+++)
    /// </summary>
    public string? StructuredCommunication { get; set; }

    /// <summary>
    /// Référence libre pour paiements sans communication structurée
    /// </summary>
    public string? Reference { get; set; }
}
