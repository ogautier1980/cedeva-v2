namespace Cedeva.Core.Enums;

/// <summary>
/// Type de dépense pour l'entité Expense.
/// Détermine si le montant est ajouté ou déduit du solde de l'animateur.
/// </summary>
public enum ExpenseType
{
    /// <summary>
    /// Note de frais - montant AJOUTÉ au solde de l'animateur (remboursement)
    /// Exemples: transport, achats de matériel pour l'activité, etc.
    /// </summary>
    Reimbursement,

    /// <summary>
    /// Consommation personnelle - montant DÉDUIT du solde de l'animateur
    /// Exemples: coca du frigo, snacks, etc.
    /// </summary>
    PersonalConsumption
}
