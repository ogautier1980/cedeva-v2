namespace Cedeva.Website.Features.Financial.ViewModels;

public class TeamSalaryViewModel
{
    public int TeamMemberId { get; set; }
    public string TeamMemberName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TeamRole { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de jours de l'activité (compte des ActivityDay)
    /// </summary>
    public int DaysCount { get; set; }

    /// <summary>
    /// Défraiement journalier du membre d'équipe
    /// </summary>
    public decimal DailyCompensation { get; set; }

    /// <summary>
    /// Prestations = DaysCount × DailyCompensation
    /// </summary>
    public decimal Prestations { get; set; }

    /// <summary>
    /// Somme des notes de frais (Expense.Reimbursement)
    /// </summary>
    public decimal Reimbursements { get; set; }

    /// <summary>
    /// Nombre de notes de frais
    /// </summary>
    public int ReimbursementsCount { get; set; }

    /// <summary>
    /// Somme des consommations personnelles (Expense.PersonalConsumption)
    /// </summary>
    public decimal PersonalConsumptions { get; set; }

    /// <summary>
    /// Nombre de consommations personnelles
    /// </summary>
    public int PersonalConsumptionsCount { get; set; }

    /// <summary>
    /// Total à verser = Prestations + Reimbursements - PersonalConsumptions
    /// </summary>
    public decimal TotalToPay { get; set; }

    /// <summary>
    /// Liste des notes de frais détaillées
    /// </summary>
    public List<ExpenseDetailViewModel> ReimbursementDetails { get; set; } = new();

    /// <summary>
    /// Liste des consommations personnelles détaillées
    /// </summary>
    public List<ExpenseDetailViewModel> PersonalConsumptionDetails { get; set; } = new();
}
