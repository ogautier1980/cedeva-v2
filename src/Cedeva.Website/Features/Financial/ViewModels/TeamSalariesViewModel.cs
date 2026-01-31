namespace Cedeva.Website.Features.Financial.ViewModels;

public class TeamSalariesViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }

    public List<TeamSalaryViewModel> TeamSalaries { get; set; } = new();

    /// <summary>
    /// Total des prestations pour tous les membres d'équipe
    /// </summary>
    public decimal TotalPrestations { get; set; }

    /// <summary>
    /// Total des remboursements pour tous les membres d'équipe
    /// </summary>
    public decimal TotalReimbursements { get; set; }

    /// <summary>
    /// Total des consommations personnelles pour tous les membres d'équipe
    /// </summary>
    public decimal TotalPersonalConsumptions { get; set; }

    /// <summary>
    /// Total général à verser = TotalPrestations + TotalReimbursements - TotalPersonalConsumptions
    /// </summary>
    public decimal GrandTotal { get; set; }
}
