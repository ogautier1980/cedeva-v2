namespace Cedeva.Website.Features.Financial.ViewModels;

public class ActivityFinancialDashboardViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Revenus totaux (paiements confirmés)
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Dépenses totales (salaires + notes de frais + autres)
    /// </summary>
    public decimal TotalExpenses { get; set; }

    /// <summary>
    /// Bilan = TotalRevenue - TotalExpenses
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Nombre de paiements en attente
    /// </summary>
    public int PendingPaymentsCount { get; set; }

    /// <summary>
    /// Montant total des paiements en attente
    /// </summary>
    public decimal PendingPaymentsAmount { get; set; }

    /// <summary>
    /// Nombre d'inscriptions
    /// </summary>
    public int BookingsCount { get; set; }

    /// <summary>
    /// Nombre d'inscriptions confirmées
    /// </summary>
    public int ConfirmedBookingsCount { get; set; }

    /// <summary>
    /// Nombre de membres d'équipe
    /// </summary>
    public int TeamMembersCount { get; set; }

    /// <summary>
    /// Montant total des salaires équipe (salaire de base + remboursements - consommations)
    /// </summary>
    public decimal TeamMemberExpenses { get; set; }

    /// <summary>
    /// Dépenses organisation (carte + caisse)
    /// </summary>
    public decimal OrganizationExpenses { get; set; }
}
