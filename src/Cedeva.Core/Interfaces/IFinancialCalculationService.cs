using Cedeva.Core.Entities;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service for calculating financial metrics for activities.
/// </summary>
public interface IFinancialCalculationService
{
    /// <summary>
    /// Calculates total revenue from confirmed (paid) payments for an activity.
    /// </summary>
    decimal CalculateTotalRevenue(Activity activity);

    /// <summary>
    /// Calculates organization expenses (expenses not assigned to team members).
    /// </summary>
    decimal CalculateOrganizationExpenses(IEnumerable<Expense> expenses);

    /// <summary>
    /// Calculates estimated team member salaries for an activity.
    /// Formula: (days × daily compensation) + reimbursements - personal consumptions
    /// </summary>
    decimal CalculateTeamMemberSalaries(Activity activity, IEnumerable<Expense> expenses);

    /// <summary>
    /// Calculates total expenses (organization + team member salaries).
    /// </summary>
    decimal CalculateTotalExpenses(Activity activity, IEnumerable<Expense> expenses);

    /// <summary>
    /// Calculates pending payment amount (NotPaid + PartiallyPaid bookings).
    /// </summary>
    decimal CalculatePendingPayments(Activity activity);

    /// <summary>
    /// Calculates net profit/loss for an activity.
    /// Formula: revenue - expenses
    /// </summary>
    decimal CalculateNetProfit(Activity activity, IEnumerable<Expense> expenses);

    /// <summary>
    /// Calculates salary for a specific team member.
    /// Formula: (days × daily compensation) + reimbursements - personal consumptions
    /// </summary>
    decimal CalculateTeamMemberSalary(TeamMember teamMember, int daysCount, IEnumerable<Expense> memberExpenses);
}
