using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
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
    public async Task Create_WithOffBalanceCategoryType_PersistsCategoryType()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx => { org = TestData.Organisation(); ctx.Add(org); return 0; });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync("/ExpenseCategories/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = "Virement caisse -> banque",
                ["CategoryType"] = ((int)ExpenseCategoryType.OffBalance).ToString()
            }));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var category = await db.ExpenseCategories.IgnoreQueryFilters()
            .SingleAsync(c => c.OrganisationId == org.Id && c.Name == "Virement caisse -> banque");
        category.CategoryType.Should().Be(ExpenseCategoryType.OffBalance);
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

    // ---- P.A.F. (Thomas, 19/11/2026): a category of its own that also takes registration payments

    private static (int OrgId, int PafId) SeedPafCategory(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        ExpenseCategory paf = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            paf = new ExpenseCategory { OrganisationId = org.Id, Name = "P.A.F.", CategoryType = ExpenseCategoryType.Income, IsRegistrationFees = true };
            ctx.ExpenseCategories.Add(paf);
            return 0;
        });
        return (org.Id, paf.Id);
    }

    [Fact]
    public async Task Index_CreatesThePafCategory_AndShowsItAsAutomaticWithoutDeleteButton()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx => { org = TestData.Organisation(); ctx.Add(org); return 0; });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var html = await client.GetStringAsync("/ExpenseCategories");

        using var db = factory.NewDbContext();
        var paf = await db.ExpenseCategories.IgnoreQueryFilters().SingleAsync(c => c.OrganisationId == org.Id && c.IsRegistrationFees);
        paf.Name.Should().Be("P.A.F.");
        paf.CategoryType.Should().Be(ExpenseCategoryType.Income);
        html.Should().Contain("Automatique");
        html.Should().NotContain($"/ExpenseCategories/Delete/{paf.Id}");
    }

    [Fact]
    public async Task Delete_PafCategory_IsRefused()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, pafId) = SeedPafCategory(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync($"/ExpenseCategories/Delete/{pafId}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var db = factory.NewDbContext();
        (await db.ExpenseCategories.IgnoreQueryFilters().AnyAsync(c => c.Id == pafId)).Should().BeTrue();
    }

    [Fact]
    public async Task Edit_PafCategory_KeepsItAnIncome_ButSavesNameAndBudget()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, pafId) = SeedPafCategory(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ExpenseCategories/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = pafId.ToString(),
            ["Name"] = "Participation aux frais",
            ["CategoryType"] = nameof(ExpenseCategoryType.Expense),
            ["Budget"] = "1500"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var db = factory.NewDbContext();
        var paf = await db.ExpenseCategories.IgnoreQueryFilters().SingleAsync(c => c.Id == pafId);
        paf.Name.Should().Be("Participation aux frais");
        paf.Budget.Should().Be(1500m);
        paf.CategoryType.Should().Be(ExpenseCategoryType.Income);
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
        (await client.GetAsync($"/Financial?id={activity.Id}")).StatusCode.Should().Be(HttpStatusCode.Found);

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
