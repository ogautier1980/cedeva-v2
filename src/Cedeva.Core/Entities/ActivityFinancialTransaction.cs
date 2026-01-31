using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

/// <summary>
/// Transaction financière liée à une activité (revenus ou dépenses).
/// Utilisée pour enregistrer tous les mouvements financiers sauf les salaires équipe
/// (qui sont calculés dynamiquement).
/// </summary>
public class ActivityFinancialTransaction
{
    public int Id { get; set; }

    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public DateTime TransactionDate { get; set; }

    public TransactionType Type { get; set; }

    /// <summary>
    /// Catégorie extensible pour supporter futures excursions
    /// (ExcursionPayment, TransportCost, TicketCost, etc.)
    /// </summary>
    public TransactionCategory Category { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = null!;

    /// <summary>
    /// FK si c'est un paiement de réservation
    /// </summary>
    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    /// <summary>
    /// FK si c'est une note de frais ou consommation personnelle
    /// </summary>
    public int? ExpenseId { get; set; }
    public Expense? Expense { get; set; }

    public int CreatedByUserId { get; set; }
}
