using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage for manageable expense categories (Lot 5): CRUD on the categories page, rename keeps
/// existing expenses consistent, and typing a new category in the expense form creates it on the fly.
/// </summary>
[Collection("WebApp")]
public class ExpenseCategoryTests
{
    [Fact]
    public async Task Create_PersistsCategory_AndRejectsDuplicate()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx => { org = TestData.Organisation(); ctx.Add(org); return 0; });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var first = await client.PostAsync("/ExpenseCategories/Create",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Goûter" }));
        first.StatusCode.Should().Be(HttpStatusCode.Found);

        using (var db = factory.NewDbContext())
            (await db.ExpenseCategories.IgnoreQueryFilters().CountAsync(c => c.Name == "Goûter")).Should().Be(1);

        // Duplicate -> re-render (200), no second row.
        var dup = await client.PostAsync("/ExpenseCategories/Create",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Goûter" }));
        dup.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var db = factory.NewDbContext())
            (await db.ExpenseCategories.IgnoreQueryFilters().CountAsync(c => c.OrganisationId == org.Id && c.Name == "Goûter")).Should().Be(1);
    }

    [Fact]
    public async Task Edit_Rename_UpdatesExistingExpensesWithOldName()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int categoryId = 0;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            ctx.SaveChanges();
            var cat = new ExpenseCategory { OrganisationId = org.Id, Name = "Transport" };
            ctx.ExpenseCategories.Add(cat);
            ctx.Expenses.Add(new Expense { ActivityId = activity.Id, Label = "Bus", Amount = 50m, Category = "Transport", ExpenseDate = new DateTime(2026, 7, 1) });
            ctx.SaveChanges();
            categoryId = cat.Id;
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync("/ExpenseCategories/Edit",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Id"] = categoryId.ToString(), ["Name"] = "Déplacements" }));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        (await db.ExpenseCategories.IgnoreQueryFilters().FirstAsync(c => c.Id == categoryId)).Name.Should().Be("Déplacements");
        (await db.Expenses.IgnoreQueryFilters().FirstAsync(e => e.Label == "Bus")).Category
            .Should().Be("Déplacements", "existing expenses follow the rename");
    }

    [Fact]
    public async Task Delete_RemovesCategory()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        int categoryId = 0;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            var cat = new ExpenseCategory { OrganisationId = org.Id, Name = "Divers" };
            ctx.ExpenseCategories.Add(cat);
            ctx.SaveChanges();
            categoryId = cat.Id;
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync($"/ExpenseCategories/Delete/{categoryId}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var db = factory.NewDbContext();
        (await db.ExpenseCategories.IgnoreQueryFilters().AnyAsync(c => c.Id == categoryId)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateExpense_WithNewCategory_CreatesTheCategoryOnTheFly()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        // Select the activity (sets the session activity the expense form relies on).
        (await client.GetAsync($"/Financial?id={activity.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsync("/Financial/CreateExpense",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Label"] = "Pique-nique",
                ["Amount"] = "30",
                ["Category"] = "Sorties",
                ["AssignedTo"] = "OrganizationCard",
                ["ExpenseDate"] = "2026-07-01",
                ["ActivityId"] = activity.Id.ToString()
            }));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        (await db.ExpenseCategories.IgnoreQueryFilters().AnyAsync(c => c.OrganisationId == org.Id && c.Name == "Sorties"))
            .Should().BeTrue("a new category typed in the expense form is created on the fly");
    }
}
