using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage for Lot D (backlog 2026-07-31): "Hors bilan" expense category excluded from the
/// Entrées/Sorties totals, and the per-activity ticket number shared between Payment and Expense.
/// </summary>
[Collection("WebApp")]
public class FinancialHorsBilanAndTicketNumberTests
{
    [Fact]
    public async Task Transactions_ExcludesOffBalanceExpenseFromTotals_ButStillDisplaysIt()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Hors Bilan");
            ctx.AddRange(org, activity);
            ctx.SaveChanges();

            var offBalanceCategory = new ExpenseCategory
            {
                OrganisationId = org.Id,
                Name = "Virement caisse -> banque",
                CategoryType = ExpenseCategoryType.OffBalance
            };
            ctx.ExpenseCategories.Add(offBalanceCategory);
            ctx.SaveChanges();

            ctx.Expenses.AddRange(
                new Expense
                {
                    ActivityId = activity.Id,
                    Label = "Materiel",
                    Amount = 20m,
                    ExpenseDate = new DateTime(2026, 7, 1),
                    OrganizationPaymentSource = "OrganizationCard"
                },
                new Expense
                {
                    ActivityId = activity.Id,
                    Label = "Virement",
                    Amount = 500m,
                    ExpenseDate = new DateTime(2026, 7, 2),
                    ExpenseCategoryId = offBalanceCategory.Id,
                    OrganizationPaymentSource = "OrganizationCash"
                });
            ctx.SaveChanges();
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        (await client.GetAsync($"/Financial?id={activity.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/Financial/Transactions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();

        // TotalExpenses card shows only the regular expense (20), the off-balance transfer (500) is excluded.
        html.Should().Contain("20,00", "the regular expense enters the Sorties total");
        // The off-balance amount is still shown, in its own "Hors bilan" section.
        html.Should().Contain("500,00", "the off-balance transfer is still displayed, just not in Entrées/Sorties");
        html.Should().Contain("Hors bilan", "the off-balance summary card/badge renders because TotalOffBalance != 0");
    }

    [Fact]
    public async Task Payment_And_Expense_ShareSequentialTicketNumbers_ResetPerActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activityOne = null!;
        Activity activityTwo = null!;
        Booking booking = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activityOne = TestData.Activity(org, "Activité 1");
            activityTwo = TestData.Activity(org, "Activité 2");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activityOne, group: null, totalAmount: 200m, paidAmount: 0m);
            ctx.AddRange(org, activityOne, activityTwo, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        // Activity 1: first expense -> ticket #1.
        (await client.GetAsync($"/Financial?id={activityOne.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/Financial/CreateExpense", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Label"] = "Fournitures",
            ["Amount"] = "10",
            ["AssignedTo"] = "OrganizationCard",
            ["ExpenseDate"] = "2026-07-01",
            ["ActivityId"] = activityOne.Id.ToString()
        }))).StatusCode.Should().Be(HttpStatusCode.Found);

        // Same activity: a payment on its booking -> ticket #2 (shared sequence with Expense).
        (await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["BookingId"] = booking.Id.ToString(),
            ["Amount"] = "50",
            ["PaymentDate"] = "2026-07-02",
            ["PaymentMethod"] = ((int)PaymentMethod.Cash).ToString(),
        }))).StatusCode.Should().Be(HttpStatusCode.Found);

        // Switch to activity 2: its own expense sequence starts fresh at #1.
        (await client.GetAsync($"/Financial?id={activityTwo.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/Financial/CreateExpense", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Label"] = "Autre dépense",
            ["Amount"] = "5",
            ["AssignedTo"] = "OrganizationCash",
            ["ExpenseDate"] = "2026-07-03",
            ["ActivityId"] = activityTwo.Id.ToString()
        }))).StatusCode.Should().Be(HttpStatusCode.Found);

        await using var db = factory.NewDbContext();
        (await db.Expenses.IgnoreQueryFilters().SingleAsync(e => e.Label == "Fournitures")).TicketNumber.Should().Be(1);
        (await db.Payments.IgnoreQueryFilters().SingleAsync(p => p.BookingId == booking.Id)).TicketNumber.Should().Be(2);
        (await db.Expenses.IgnoreQueryFilters().SingleAsync(e => e.Label == "Autre dépense")).TicketNumber.Should().Be(1);
    }
}
