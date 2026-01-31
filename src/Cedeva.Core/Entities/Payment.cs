using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

/// <summary>
/// Paiement effectué pour une réservation (virement bancaire ou cash).
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

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

    /// <summary>
    /// FK vers BankTransaction si le paiement provient d'un import CODA
    /// </summary>
    public int? BankTransactionId { get; set; }
    public BankTransaction? BankTransaction { get; set; }

    /// <summary>
    /// Utilisateur qui a enregistré le paiement (pour paiements cash manuels)
    /// </summary>
    public int? CreatedByUserId { get; set; }
}
