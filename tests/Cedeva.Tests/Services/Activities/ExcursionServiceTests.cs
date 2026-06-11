using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Services.Activities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Services.Activities;

public class ExcursionServiceTests
{
    private sealed record Scenario(SqliteTestContext Db, int ExcursionId, int BookingId, int ActivityId);

    private static Scenario Seed(decimal cost = 10m, decimal total = 100m, decimal paid = 50m,
        bool eligibleGroup = true, bool withGroup = true)
    {
        var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var targetGroup = TestData.Group(activity, "Lions");
        var otherGroup = TestData.Group(activity, "Tigres");
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);

        ActivityGroup? bookingGroup = withGroup ? (eligibleGroup ? targetGroup : otherGroup) : null;
        var booking = TestData.Booking(child, activity, bookingGroup, total, paid);

        var excursion = TestData.Excursion(activity, cost);
        var link = TestData.ExcursionGroup(excursion, targetGroup);

        ctx.AddRange(org, activity, targetGroup, otherGroup, parent, child, booking, excursion, link);
        ctx.SaveChanges();

        return new Scenario(db, excursion.Id, booking.Id, activity.Id);
    }

    [Fact]
    public async Task RegisterChild_Eligible_AddsCostToBookingAndCreatesIncomeTransaction()
    {
        var s = Seed(cost: 10m, total: 100m, paid: 50m);
        using var _ = s.Db;
        var sut = new ExcursionService(s.Db.Context);

        var registration = await sut.RegisterChildAsync(s.ExcursionId, s.BookingId);

        registration.Should().NotBeNull();
        registration.ExcursionId.Should().Be(s.ExcursionId);
        registration.BookingId.Should().Be(s.BookingId);

        await using var verify = s.Db.NewContext();
        var booking = await verify.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.TotalAmount.Should().Be(110m);
        booking.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);

        var tx = await verify.ActivityFinancialTransactions.SingleAsync();
        tx.Amount.Should().Be(10m);
        tx.Type.Should().Be(TransactionType.Income);
        tx.Category.Should().Be(TransactionCategory.ExcursionPayment);
        tx.ActivityId.Should().Be(s.ActivityId);
    }

    [Theory]
    [InlineData(100, 0, 10, PaymentStatus.NotPaid)]      // paid 0
    [InlineData(100, 50, 10, PaymentStatus.PartiallyPaid)] // 50 < 110
    [InlineData(100, 110, 10, PaymentStatus.Paid)]       // 110 == 110
    [InlineData(100, 200, 10, PaymentStatus.Overpaid)]   // 200 > 110
    public async Task RegisterChild_RecalculatesPaymentStatus(decimal total, decimal paid, decimal cost, PaymentStatus expected)
    {
        var s = Seed(cost: cost, total: total, paid: paid);
        using var _ = s.Db;
        var sut = new ExcursionService(s.Db.Context);

        await sut.RegisterChildAsync(s.ExcursionId, s.BookingId);

        await using var verify = s.Db.NewContext();
        var booking = await verify.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.PaymentStatus.Should().Be(expected);
    }

    [Fact]
    public async Task RegisterChild_IneligibleGroup_Throws()
    {
        var s = Seed(eligibleGroup: false);
        using var _ = s.Db;
        var sut = new ExcursionService(s.Db.Context);

        var act = async () => await sut.RegisterChildAsync(s.ExcursionId, s.BookingId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not eligible*");
    }

    [Fact]
    public async Task RegisterChild_BookingWithoutGroup_Throws()
    {
        var s = Seed(withGroup: false);
        using var _ = s.Db;
        var sut = new ExcursionService(s.Db.Context);

        var act = async () => await sut.RegisterChildAsync(s.ExcursionId, s.BookingId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RegisterChild_Duplicate_Throws()
    {
        var s = Seed();
        using var _ = s.Db;
        var sut = new ExcursionService(s.Db.Context);

        await sut.RegisterChildAsync(s.ExcursionId, s.BookingId);

        var act = async () => await sut.RegisterChildAsync(s.ExcursionId, s.BookingId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public async Task RegisterChild_UnknownExcursion_Throws()
    {
        var s = Seed();
        using var _ = s.Db;
        var sut = new ExcursionService(s.Db.Context);

        var act = async () => await sut.RegisterChildAsync(99999, s.BookingId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Excursion not found*");
    }

    [Fact]
    public async Task Unregister_ReversesTotalAndCreatesNegativeTransaction()
    {
        var s = Seed(cost: 10m, total: 100m, paid: 50m);
        using var _ = s.Db;
        var sut = new ExcursionService(s.Db.Context);
        await sut.RegisterChildAsync(s.ExcursionId, s.BookingId); // total -> 110

        var result = await sut.UnregisterChildAsync(s.ExcursionId, s.BookingId);

        result.Should().BeTrue();

        await using var verify = s.Db.NewContext();
        var booking = await verify.Bookings.SingleAsync(b => b.Id == s.BookingId);
        booking.TotalAmount.Should().Be(100m); // back to original

        (await verify.ExcursionRegistrations.AnyAsync(r => r.BookingId == s.BookingId))
            .Should().BeFalse();

        var transactions = await verify.ActivityFinancialTransactions.ToListAsync();
        transactions.Should().HaveCount(2); // income + reversal
        transactions.Should().ContainSingle(t => t.Amount == -10m);
    }

    [Fact]
    public async Task Unregister_NotRegistered_ReturnsFalse()
    {
        var s = Seed();
        using var _ = s.Db;
        var sut = new ExcursionService(s.Db.Context);

        (await sut.UnregisterChildAsync(s.ExcursionId, s.BookingId)).Should().BeFalse();
    }

    [Fact]
    public async Task GetFinancialSummary_ComputesRevenueExpensesAndNet()
    {
        var s = Seed(cost: 10m);
        using var _ = s.Db;
        var sut = new ExcursionService(s.Db.Context);
        await sut.RegisterChildAsync(s.ExcursionId, s.BookingId);

        // Add an excursion expense of 4
        await using (var seedCtx = s.Db.NewContext())
        {
            seedCtx.Expenses.Add(new Expense
            {
                Label = "Transport",
                Amount = 4m,
                ActivityId = s.ActivityId,
                ExcursionId = s.ExcursionId,
                ExpenseDate = new DateTime(2026, 7, 3)
            });
            await seedCtx.SaveChangesAsync();
        }

        var summary = await sut.GetFinancialSummaryAsync(s.ExcursionId);

        summary.RegistrationCount.Should().Be(1);
        summary.TotalRevenue.Should().Be(10m);
        summary.TotalExpenses.Should().Be(4m);
        summary.NetBalance.Should().Be(6m);
    }
}
