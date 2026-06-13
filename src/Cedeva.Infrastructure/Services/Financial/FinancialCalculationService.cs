using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;

namespace Cedeva.Infrastructure.Services.Financial;

/// <summary>
/// Service for calculating financial metrics for activities.
/// Extracted from controllers to improve testability and reusability.
/// </summary>
public class FinancialCalculationService : IFinancialCalculationService
{
    public decimal CalculateTotalRevenue(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return activity.Bookings
            .SelectMany(b => b.Payments)
            .Where(p => p.Status == PaymentStatus.Paid)
            .Sum(p => p.Amount);
    }

    public decimal CalculateOrganizationExpenses(IEnumerable<Expense> expenses)
    {
        ArgumentNullException.ThrowIfNull(expenses);

        return expenses
            .Where(e => !e.TeamMemberId.HasValue)
            .Sum(e => e.Amount);
    }

    public decimal CalculateTeamMemberSalaries(Activity activity, IEnumerable<Expense> expenses)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(expenses);

        var daysCount = activity.Days.Count;
        var expensesList = expenses.ToList();

        return activity.TeamMembers.Sum(tm =>
            CalculateTeamMemberSalary(tm, daysCount, expensesList.Where(e => e.TeamMemberId == tm.TeamMemberId))
        );
    }

    public decimal CalculateTeamMemberSalary(TeamMember teamMember, int daysCount, IEnumerable<Expense> memberExpenses)
    {
        ArgumentNullException.ThrowIfNull(teamMember);
        ArgumentNullException.ThrowIfNull(memberExpenses);

        var baseSalary = daysCount * (teamMember.DailyCompensation ?? 0);
        var expensesList = memberExpenses.ToList();

        var reimbursements = expensesList
            .Where(e => e.ExpenseType == ExpenseType.Reimbursement)
            .Sum(e => e.Amount);

        var consumptions = expensesList
            .Where(e => e.ExpenseType == ExpenseType.PersonalConsumption)
            .Sum(e => e.Amount);

        return baseSalary + reimbursements - consumptions;
    }

    public decimal CalculateTotalExpenses(Activity activity, IEnumerable<Expense> expenses)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(expenses);

        var organizationExpenses = CalculateOrganizationExpenses(expenses);
        var teamMemberSalaries = CalculateTeamMemberSalaries(activity, expenses);

        return organizationExpenses + teamMemberSalaries;
    }

    public decimal CalculatePendingPayments(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return activity.Bookings
            .Where(b => b.PaymentStatus == PaymentStatus.NotPaid ||
                       b.PaymentStatus == PaymentStatus.PartiallyPaid)
            .Sum(b => b.TotalAmount - b.PaidAmount);
    }

    public decimal CalculateNetProfit(Activity activity, IEnumerable<Expense> expenses)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(expenses);

        var revenue = CalculateTotalRevenue(activity);
        var totalExpenses = CalculateTotalExpenses(activity, expenses);

        return revenue - totalExpenses;
    }
}
