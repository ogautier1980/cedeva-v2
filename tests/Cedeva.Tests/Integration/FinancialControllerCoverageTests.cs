using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage tests for <c>FinancialController</c> actions NOT already covered by
/// <c>FinancialControllerIntegrationTests</c> (which only exercises Index happy/tenant/no-auth).
///
/// Most actions read the "current activity" from the session
/// (key <c>Financial_ActivityId</c>). The controller seeds that key when Index is hit with
/// <c>?id=</c>. We therefore "select" the activity by GETting <c>/Financial?id={id}</c> on a
/// client first; the session cookie is retained by the HttpClient and reused on later requests.
/// </summary>
[Collection("WebApp")]
public class FinancialControllerCoverageTests
{
    private sealed record Graph(int OrgId, int ActivityId, int TeamMemberId, int ExpenseId);

    /// <summary>
    /// Seeds an organisation + activity with two days, one team member (assigned to the activity),
    /// a confirmed booking with a Paid payment, plus one organisation expense and one team-member
    /// reimbursement expense.
    /// </summary>
    private static Graph SeedFullGraph(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        Activity activity = null!;
        TeamMember teamMember = null!;
        Expense orgExpense = null!;

        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Coverage");

            activity.Days.Add(new ActivityDay
            {
                Label = "Jour 1",
                DayDate = new DateTime(2026, 7, 1),
                IsActive = true,
                Activity = activity
            });
            activity.Days.Add(new ActivityDay
            {
                Label = "Jour 2",
                DayDate = new DateTime(2026, 7, 2),
                IsActive = true,
                Activity = activity
            });

            teamMember = new TeamMember
            {
                FirstName = "Anna",
                LastName = "Animatrice",
                Email = "anna@test.be",
                BirthDate = new DateTime(1990, 1, 1),
                Address = TestData.Address(),
                MobilePhoneNumber = "0470000001",
                NationalRegisterNumber = "90010112345",
                TeamRole = TeamRole.Animator,
                License = License.License,
                Status = Status.Compensated,
                DailyCompensation = 50m,
                LicenseUrl = "license.pdf",
                Organisation = org
            };
            activity.TeamMembers.Add(teamMember);

            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 100m);
            booking.PaymentStatus = PaymentStatus.Paid;

            var payment = new Payment
            {
                Booking = booking,
                Amount = 100m,
                Status = PaymentStatus.Paid,
                PaymentMethod = PaymentMethod.Cash,
                PaymentDate = new DateTime(2026, 6, 1)
            };

            orgExpense = new Expense
            {
                Label = "Materiel",
                Amount = 30m,
                Category = "Fournitures",
                ExpenseDate = new DateTime(2026, 6, 2),
                OrganizationPaymentSource = "OrganizationCard",
                Activity = activity
            };

            var tmExpense = new Expense
            {
                Label = "Transport",
                Amount = 15m,
                ExpenseDate = new DateTime(2026, 6, 3),
                ExpenseType = ExpenseType.Reimbursement,
                TeamMember = teamMember,
                Activity = activity
            };

            ctx.AddRange(org, activity, parent, child, booking, payment, orgExpense, tmExpense);
            return 0;
        });

        return new Graph(org.Id, activity.Id, teamMember.TeamMemberId, orgExpense.Id);
    }

    /// <summary>Creates a client whose session already has the activity selected.</summary>
    private static async Task<HttpClient> ClientWithActivitySelected(
        CedevaWebApplicationFactory factory, int orgId, int activityId, string role = "Coordinator")
    {
        var client = factory.CreateClientFor("u1", orgId, role);
        var select = await client.GetAsync($"/Financial?id={activityId}");
        select.StatusCode.Should().Be(HttpStatusCode.OK); // confirms session seeded
        return client;
    }

    // ---------------------------------------------------------------------
    // BeginFinancial (POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BeginFinancial_SetsSessionAndRedirectsToTransactions()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = factory.CreateClientFor("u1", g.OrgId, "Coordinator");

        var response = await client.PostAsync("/Financial/BeginFinancial",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = g.ActivityId.ToString() }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        // RedirectToAction(nameof(Transactions)) on the Financial controller: "Comptes" skips the
        // intermediate dashboard and lands straight on the transaction list.
        response.Headers.Location!.ToString().Should().Be("/Financial/Transactions");

        // Following the redirect on the same client (session cookie carries the activity id)
        // must render the transaction list, proving the session was set.
        var transactions = await client.GetAsync("/Financial/Transactions");
        transactions.StatusCode.Should().Be(HttpStatusCode.OK);
        (await transactions.Content.ReadAsStringAsync()).Should().Contain("Stage Coverage");
    }

    [Fact]
    public async Task BeginFinancial_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/Financial/BeginFinancial",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "1" }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // Index – activity selected but belongs to another org (session set, tenancy filter excludes)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_SelectedActivityFromAnotherOrg_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);

        // Coordinator of a different org. Session gets the id set, but the tenancy filter
        // hides the activity, so the controller returns NotFound.
        var client = factory.CreateClientFor("u1", organisationId: 99999, role: "Coordinator");
        var response = await client.GetAsync($"/Financial?id={g.ActivityId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // TeamSalaries (GET)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TeamSalaries_WithoutSelectedActivity_RedirectsToActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/Financial/TeamSalaries");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");
    }

    [Fact]
    public async Task TeamSalaries_WithSelectedActivity_RendersTeamMember()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync("/Financial/TeamSalaries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Animatrice");
    }

    [Fact]
    public async Task TeamSalaries_OnlyPaysForDaysMarkedPresent()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory); // 2 ActivityDays, Anna Animatrice @ 50/day, no presence marked yet

        // Mark present on day 1 only, absent on day 2 -> expect 1*50 = 50 in Prestations, not 2*50 = 100.
        factory.Seed(ctx =>
        {
            var days = ctx.ActivityDays.IgnoreQueryFilters()
                .Where(d => d.ActivityId == g.ActivityId).OrderBy(d => d.DayDate).ToList();
            ctx.TeamMemberDays.Add(new TeamMemberDay { ActivityDayId = days[0].DayId, TeamMemberId = g.TeamMemberId, IsPresent = true });
            ctx.TeamMemberDays.Add(new TeamMemberDay { ActivityDayId = days[1].DayId, TeamMemberId = g.TeamMemberId, IsPresent = false });
            return 0;
        });

        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync("/Financial/TeamSalaries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // Prestations = 1 present day * 50 = 50,00 (not 100,00 for both days).
        html.Should().Contain("50,00");
        html.Should().NotContain("100,00");
    }

    // ---------------------------------------------------------------------
    // ExportTeamSalaries (GET) – Excel
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExportTeamSalaries_WithoutSelectedActivity_RedirectsToActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/Financial/ExportTeamSalaries");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");
    }

    [Fact]
    public async Task ExportTeamSalaries_WithSelectedActivity_ReturnsNonEmptyXlsx()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync("/Financial/ExportTeamSalaries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    // ---------------------------------------------------------------------
    // ExportExpenses (GET) – Excel
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ExportExpenses_WithoutSelectedActivity_RedirectsToActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/Financial/ExportExpenses");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");
    }

    [Fact]
    public async Task ExportExpenses_WithSelectedActivity_ReturnsNonEmptyXlsx()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync("/Financial/ExportExpenses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    // ---------------------------------------------------------------------
    // Transactions (GET) – bare list, no stats cards/filter tabs (Lot D)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Transactions_WithoutSelectedActivity_RedirectsToActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/Financial/Transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");
    }

    [Fact]
    public async Task Transactions_ShowsIncomeAndExpenses()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync("/Financial/Transactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stage Coverage");
        html.Should().Contain("Materiel");
    }

    // ---------------------------------------------------------------------
    // Report (GET)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Report_WithoutSelectedActivity_RedirectsToActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/Financial/Report");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");
    }

    [Fact]
    public async Task Report_WithSelectedActivity_RendersReport()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync("/Financial/Report");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Stage Coverage");
    }

    // ---------------------------------------------------------------------
    // AddTransaction (GET) – merged Paiement/Dépense screen
    // ---------------------------------------------------------------------

    [Fact]
    public async Task AddTransaction_WithoutSelectedActivity_RedirectsToActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/Financial/AddTransaction");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");
    }

    [Fact]
    public async Task AddTransaction_WithSelectedActivity_RendersBothTabs()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync("/Financial/AddTransaction");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // Paiement tab: booking already fully paid in SeedFullGraph, so the empty state shows.
        html.Should().Contain("payment-pane");
        html.Should().Contain("expense-pane");
    }

    [Fact]
    public async Task AddTransaction_ListsOutstandingBookingForSelectedActivityOnly()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);

        // Add a second activity in the same org with an outstanding booking.
        int otherActivityId = factory.Seed(ctx =>
        {
            var org = ctx.Organisations.IgnoreQueryFilters().Single(o => o.Id == g.OrgId);
            var otherActivity = TestData.Activity(org, "Autre Stage");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            child.LastName = "OutstandingMarker";
            var booking = TestData.Booking(child, otherActivity, group: null, totalAmount: 80m, paidAmount: 0m);
            ctx.Add(booking);
            return otherActivity;
        }).Id;

        var client = await ClientWithActivitySelected(factory, g.OrgId, otherActivityId);

        var response = await client.GetAsync("/Financial/AddTransaction");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("OutstandingMarker");
    }

    // ---------------------------------------------------------------------
    // CreateExpense (GET)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreateExpense_Get_WithoutSelectedActivity_RedirectsToActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/Financial/CreateExpense");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");
    }

    [Fact]
    public async Task CreateExpense_Get_WithSelectedActivity_RendersForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync("/Financial/CreateExpense");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // CreateExpense (POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreateExpense_Post_OrganizationCard_PersistsAndRedirectsToTransactions()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.PostAsync("/Financial/CreateExpense",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Label"] = "Achat ballons",
                ["Amount"] = "42",
                ["Category"] = "Jeux",
                ["AssignedTo"] = "OrganizationCard",
                ["ExpenseDate"] = "2026-07-01",
                ["ActivityId"] = g.ActivityId.ToString()
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/Financial/Transactions");

        await using var ctx = factory.NewDbContext();
        var created = await ctx.Expenses
            .IgnoreQueryFilters()
            .SingleAsync(e => e.Label == "Achat ballons");
        created.Amount.Should().Be(42m);
        created.OrganizationPaymentSource.Should().Be("OrganizationCard");
        created.TeamMemberId.Should().BeNull();
        created.ExpenseType.Should().BeNull();
        created.ActivityId.Should().Be(g.ActivityId);
    }

    [Fact]
    public async Task CreateExpense_Post_TeamMember_SetsReimbursementByDefault()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.PostAsync("/Financial/CreateExpense",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Label"] = "Train animatrice",
                ["Amount"] = "12",
                ["AssignedTo"] = g.TeamMemberId.ToString(),
                ["ExpenseDate"] = "2026-07-02",
                ["ActivityId"] = g.ActivityId.ToString()
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/Financial/Transactions");

        await using var ctx = factory.NewDbContext();
        var created = await ctx.Expenses
            .IgnoreQueryFilters()
            .SingleAsync(e => e.Label == "Train animatrice");
        created.TeamMemberId.Should().Be(g.TeamMemberId);
        created.OrganizationPaymentSource.Should().BeNull();
        created.ExpenseType.Should().Be(ExpenseType.Reimbursement);
    }

    [Fact]
    public async Task CreateExpense_Post_Invalid_ReRendersWithoutPersisting()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        // Missing Label and zero Amount (below [Range(0.01,...)]) => invalid model.
        var response = await client.PostAsync("/Financial/CreateExpense",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Amount"] = "0",
                ["AssignedTo"] = "OrganizationCash",
                ["ExpenseDate"] = "2026-07-01",
                ["ActivityId"] = g.ActivityId.ToString()
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-rendered form

        await using var ctx = factory.NewDbContext();
        (await ctx.Expenses.IgnoreQueryFilters().AnyAsync(e => e.OrganizationPaymentSource == "OrganizationCash"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task CreateExpense_Post_WithoutSelectedActivity_RedirectsToActivities()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.PostAsync("/Financial/CreateExpense",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Label"] = "X",
                ["Amount"] = "5",
                ["AssignedTo"] = "OrganizationCard",
                ["ExpenseDate"] = "2026-07-01"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Activities");
    }

    // ---------------------------------------------------------------------
    // EditExpense (GET)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EditExpense_Get_NonExistent_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync("/Financial/EditExpense/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditExpense_Get_Existing_RendersForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.GetAsync($"/Financial/EditExpense/{g.ExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Materiel");
    }

    [Fact]
    public async Task EditExpense_Get_DifferentActivitySelected_Forbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);

        // Seed a second activity in the SAME org and select it; the expense belongs to the first.
        int otherActivityId = factory.Seed(ctx =>
        {
            var org = ctx.Organisations.IgnoreQueryFilters().Single(o => o.Id == g.OrgId);
            var other = TestData.Activity(org, "Autre Stage");
            ctx.Add(other);
            return other;
        }).Id;

        var client = await ClientWithActivitySelected(factory, g.OrgId, otherActivityId);

        var response = await client.GetAsync($"/Financial/EditExpense/{g.ExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------
    // EditExpense (POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EditExpense_Post_Valid_UpdatesAndRedirectsToTransactions()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.PostAsync($"/Financial/EditExpense/{g.ExpenseId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = g.ExpenseId.ToString(),
                ["Label"] = "Materiel modifié",
                ["Amount"] = "55",
                ["AssignedTo"] = "OrganizationCash",
                ["ExpenseDate"] = "2026-07-05",
                ["ActivityId"] = g.ActivityId.ToString()
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/Financial/Transactions");

        await using var ctx = factory.NewDbContext();
        var updated = await ctx.Expenses.IgnoreQueryFilters().SingleAsync(e => e.Id == g.ExpenseId);
        updated.Label.Should().Be("Materiel modifié");
        updated.Amount.Should().Be(55m);
        updated.OrganizationPaymentSource.Should().Be("OrganizationCash");
    }

    [Fact]
    public async Task EditExpense_Post_WithLocalReturnUrl_RedirectsThere()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.PostAsync($"/Financial/EditExpense/{g.ExpenseId}?returnUrl=/Financial/Report",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = g.ExpenseId.ToString(),
                ["Label"] = "Materiel v2",
                ["Amount"] = "20",
                ["AssignedTo"] = "OrganizationCard",
                ["ExpenseDate"] = "2026-07-05",
                ["ActivityId"] = g.ActivityId.ToString()
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/Financial/Report");
    }

    [Fact]
    public async Task EditExpense_Post_Invalid_ReRendersWithoutChange()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.PostAsync($"/Financial/EditExpense/{g.ExpenseId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = g.ExpenseId.ToString(),
                ["Label"] = "", // required
                ["Amount"] = "0", // below range
                ["AssignedTo"] = "OrganizationCard",
                ["ExpenseDate"] = "2026-07-05",
                ["ActivityId"] = g.ActivityId.ToString()
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        var unchanged = await ctx.Expenses.IgnoreQueryFilters().SingleAsync(e => e.Id == g.ExpenseId);
        unchanged.Label.Should().Be("Materiel");
        unchanged.Amount.Should().Be(30m);
    }

    [Fact]
    public async Task EditExpense_Post_NonExistent_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.PostAsync("/Financial/EditExpense/999999",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = "999999",
                ["Label"] = "Ghost",
                ["Amount"] = "10",
                ["AssignedTo"] = "OrganizationCard",
                ["ExpenseDate"] = "2026-07-05",
                ["ActivityId"] = g.ActivityId.ToString()
            }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // DeleteExpense (POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeleteExpense_Valid_RemovesAndRedirectsToTransactions()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.PostAsync($"/Financial/DeleteExpense/{g.ExpenseId}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/Financial/Transactions");

        await using var ctx = factory.NewDbContext();
        (await ctx.Expenses.IgnoreQueryFilters().AnyAsync(e => e.Id == g.ExpenseId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteExpense_NonExistent_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);
        var client = await ClientWithActivitySelected(factory, g.OrgId, g.ActivityId);

        var response = await client.PostAsync("/Financial/DeleteExpense/999999",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteExpense_DifferentActivitySelected_Forbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        var g = SeedFullGraph(factory);

        int otherActivityId = factory.Seed(ctx =>
        {
            var org = ctx.Organisations.IgnoreQueryFilters().Single(o => o.Id == g.OrgId);
            var other = TestData.Activity(org, "Autre Stage 2");
            ctx.Add(other);
            return other;
        }).Id;

        var client = await ClientWithActivitySelected(factory, g.OrgId, otherActivityId);

        var response = await client.PostAsync($"/Financial/DeleteExpense/{g.ExpenseId}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var ctx = factory.NewDbContext();
        (await ctx.Expenses.IgnoreQueryFilters().AnyAsync(e => e.Id == g.ExpenseId)).Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // Authorization – unauthenticated across representative actions
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Transactions_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Financial/Transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExportTeamSalaries_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Financial/ExportTeamSalaries");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
