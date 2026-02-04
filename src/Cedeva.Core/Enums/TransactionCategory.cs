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
    Other,

    /// <summary>
    /// Paiement d'excursion (revenus supplémentaires)
    /// </summary>
    ExcursionPayment,

    /// <summary>
    /// Dépense liée à une excursion (transport, billets, etc.)
    /// </summary>
    ExcursionExpense
}
