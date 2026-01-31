namespace Cedeva.Website.Features.Financial.ViewModels;

public class FinancialReportViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }

    // Revenus
    public decimal TotalRevenue { get; set; }
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int PendingBookings { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal AverageRevenuePerBooking { get; set; }

    // Dépenses organisation
    public decimal OrganizationCardExpenses { get; set; }
    public decimal OrganizationCashExpenses { get; set; }
    public decimal TotalOrganizationExpenses { get; set; }
    public List<ExpenseDetailViewModel> OrganizationExpenseDetails { get; set; } = new();

    // Salaires équipe
    public int TeamMembersCount { get; set; }
    public decimal TotalTeamSalaries { get; set; }
    public List<TeamMemberSalaryDetailViewModel> TeamMemberSalaryDetails { get; set; } = new();

    // Bilan
    public decimal TotalExpenses { get; set; }
    public decimal Balance { get; set; }
    public decimal BalancePercentage { get; set; }
}

public class TeamMemberSalaryDetailViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int DaysWorked { get; set; }
    public decimal DailyCompensation { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Reimbursements { get; set; }
    public decimal PersonalConsumptions { get; set; }
    public decimal NetSalary { get; set; }
}

public class ExpenseDetailViewModel
{
    public int Id { get; set; }
    public DateTime? Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Amount { get; set; }
}
