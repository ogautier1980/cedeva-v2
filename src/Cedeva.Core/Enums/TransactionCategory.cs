namespace Cedeva.Core.Enums;

/// <summary>
/// Catégories de transactions financières.
/// Extensible pour futures excursions (ExcursionPayment, TransportCost, TicketCost, etc.)
/// </summary>
public enum TransactionCategory
{
    /// <summary>
    /// Paiement de réservation (revenus)
    /// </summary>
    Payment,

    /// <summary>
    /// Note de frais animateur - remboursement (dépense)
    /// </summary>
    TeamExpense,

    /// <summary>
    /// Consommation personnelle animateur - débit du solde (dépense)
    /// </summary>
    PersonalConsumption,

    /// <summary>
    /// Autres dépenses/revenus
    /// </summary>
    Other

    // Futures valeurs pour excursions:
    // ExcursionPayment,
    // TransportCost,
    // TicketCost,
    // MaterialCost,
    // etc.
}
