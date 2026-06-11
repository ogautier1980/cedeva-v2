using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Services.Financial;

namespace Cedeva.Tests.Services.Financial;

public class FinancialCalculationServiceTests
{
    private readonly FinancialCalculationService _sut = new();

    private static Booking BookingWithPayments(params (decimal amount, PaymentStatus status)[] payments)
    {
        var booking = new Booking();
        foreach (var (amount, status) in payments)
            booking.Payments.Add(new Payment { Amount = amount, Status = status });
        return booking;
    }

    [Fact]
    public void CalculateTotalRevenue_SumsOnlyPaidPayments()
    {
        var activity = new Activity();
        activity.Bookings.Add(BookingWithPayments(
            (100m, PaymentStatus.Paid),
            (50m, PaymentStatus.PartiallyPaid),   // ignored
            (25m, PaymentStatus.NotPaid)));        // ignored
        activity.Bookings.Add(BookingWithPayments((200m, PaymentStatus.Paid)));

        _sut.CalculateTotalRevenue(activity).Should().Be(300m);
    }

    [Fact]
    public void CalculateTotalRevenue_WithNoPayments_ReturnsZero()
    {
        _sut.CalculateTotalRevenue(new Activity()).Should().Be(0m);
    }

    [Fact]
    public void CalculateTotalRevenue_WithNullActivity_Throws()
    {
        var act = () => _sut.CalculateTotalRevenue(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalculateOrganizationExpenses_SumsOnlyNonTeamMemberExpenses()
    {
        var expenses = new List<Expense>
        {
            new() { Amount = 40m, TeamMemberId = null },   // org
            new() { Amount = 60m, TeamMemberId = null },   // org
            new() { Amount = 999m, TeamMemberId = 5 }      // team member -> ignored
        };

        _sut.CalculateOrganizationExpenses(expenses).Should().Be(100m);
    }

    [Fact]
    public void CalculateTeamMemberSalary_AppliesFormula()
    {
        // (days * dailyCompensation) + reimbursements - personalConsumptions
        var tm = new TeamMember { TeamMemberId = 1, DailyCompensation = 30m };
        var expenses = new List<Expense>
        {
            new() { Amount = 15m, ExpenseType = ExpenseType.Reimbursement },
            new() { Amount = 5m, ExpenseType = ExpenseType.PersonalConsumption }
        };

        // (4 * 30) + 15 - 5 = 130
        _sut.CalculateTeamMemberSalary(tm, daysCount: 4, expenses).Should().Be(130m);
    }

    [Fact]
    public void CalculateTeamMemberSalary_WithNullDailyCompensation_TreatsAsZero()
    {
        var tm = new TeamMember { TeamMemberId = 1, DailyCompensation = null };

        _sut.CalculateTeamMemberSalary(tm, daysCount: 10, Enumerable.Empty<Expense>())
            .Should().Be(0m);
    }

    [Fact]
    public void CalculateTeamMemberSalaries_SumsPerMemberAndScopesExpensesByMember()
    {
        var activity = new Activity();
        activity.Days.Add(new ActivityDay());
        activity.Days.Add(new ActivityDay()); // 2 days
        activity.TeamMembers.Add(new TeamMember { TeamMemberId = 1, DailyCompensation = 10m });
        activity.TeamMembers.Add(new TeamMember { TeamMemberId = 2, DailyCompensation = 20m });

        var expenses = new List<Expense>
        {
            new() { Amount = 5m, ExpenseType = ExpenseType.Reimbursement, TeamMemberId = 1 },
            new() { Amount = 100m, ExpenseType = ExpenseType.Reimbursement, TeamMemberId = 2 },
            new() { Amount = 999m, TeamMemberId = null } // org expense -> not part of salaries
        };

        // tm1: (2*10)+5 = 25 ; tm2: (2*20)+100 = 140 ; total 165
        _sut.CalculateTeamMemberSalaries(activity, expenses).Should().Be(165m);
    }

    [Fact]
    public void CalculateTotalExpenses_CombinesOrgAndTeamMember()
    {
        var activity = new Activity();
        activity.Days.Add(new ActivityDay());
        activity.TeamMembers.Add(new TeamMember { TeamMemberId = 1, DailyCompensation = 10m });

        var expenses = new List<Expense>
        {
            new() { Amount = 50m, TeamMemberId = null },                               // org
            new() { Amount = 5m, ExpenseType = ExpenseType.Reimbursement, TeamMemberId = 1 } // member
        };

        // org 50 + salary ((1*10)+5)=15 => 65
        _sut.CalculateTotalExpenses(activity, expenses).Should().Be(65m);
    }

    [Fact]
    public void CalculatePendingPayments_SumsRemainderForUnpaidAndPartial()
    {
        var activity = new Activity();
        activity.Bookings.Add(new Booking { PaymentStatus = PaymentStatus.NotPaid, TotalAmount = 100m, PaidAmount = 0m });
        activity.Bookings.Add(new Booking { PaymentStatus = PaymentStatus.PartiallyPaid, TotalAmount = 100m, PaidAmount = 40m });
        activity.Bookings.Add(new Booking { PaymentStatus = PaymentStatus.Paid, TotalAmount = 100m, PaidAmount = 100m }); // ignored

        // 100 + 60 = 160
        _sut.CalculatePendingPayments(activity).Should().Be(160m);
    }

    [Fact]
    public void CalculateNetProfit_IsRevenueMinusTotalExpenses()
    {
        var activity = new Activity();
        activity.Days.Add(new ActivityDay());
        activity.TeamMembers.Add(new TeamMember { TeamMemberId = 1, DailyCompensation = 10m });
        activity.Bookings.Add(BookingWithPayments((200m, PaymentStatus.Paid)));

        var expenses = new List<Expense>
        {
            new() { Amount = 50m, TeamMemberId = null } // org expense
        };

        // revenue 200 - (org 50 + salary 10) = 140
        _sut.CalculateNetProfit(activity, expenses).Should().Be(140m);
    }
}
