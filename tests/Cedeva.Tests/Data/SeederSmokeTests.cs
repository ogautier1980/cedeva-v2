using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Data;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cedeva.Tests.Data;

/// <summary>
/// Smoke / integration coverage for <see cref="TestDataSeeder"/> run against a real EF Core
/// schema on SQLite. This exercises a large surface of the seeding code (parents, children,
/// team members, activities + days/groups/questions, activity↔team assignments, bookings,
/// booking days, question answers, payments, expenses, excursions and email templates) and
/// asserts the representative tables are populated without error.
///
/// <para><see cref="DbSeeder"/> is intentionally NOT exercised here: it requires ASP.NET
/// Identity <c>UserManager</c>/<c>RoleManager</c>, <c>Database.MigrateAsync()</c> (relational
/// migrations, incompatible with EnsureCreated on in-memory SQLite) and a physical
/// <c>municipalities.csv</c> on disk — none trivially constructible in isolation.</para>
/// </summary>
public class SeederSmokeTests
{
    private static TestDataSeeder CreateSeeder(SqliteTestContext db) =>
        new(db.Context, NullLogger<TestDataSeeder>.Instance);

    /// <summary>Adds a single persisted organisation (the only precondition TestDataSeeder requires).</summary>
    private static Organisation SeedOrganisation(SqliteTestContext db, string name = "Org Seed")
    {
        var org = TestData.Organisation(name);
        // Clear the bank account so EnsureOrganisationBankAccountAsync's population branch runs.
        org.BankAccountNumber = null;
        org.BankAccountName = null;
        db.Context.Add(org);
        db.Context.SaveChanges();
        return org;
    }

    /// <summary>
    /// Adds a non-Identity-managed CedevaUser straight to the context so the
    /// SeedEmailTemplatesAsync branch (which short-circuits when no users exist) executes.
    /// </summary>
    private static void SeedUser(SqliteTestContext db, int organisationId)
    {
        db.Context.Users.Add(new CedevaUser
        {
            Id = "seed-user-1",
            UserName = "coord@example.be",
            NormalizedUserName = "COORD@EXAMPLE.BE",
            Email = "coord@example.be",
            NormalizedEmail = "COORD@EXAMPLE.BE",
            FirstName = "Coord",
            LastName = "Test",
            Role = Cedeva.Core.Enums.Role.Coordinator,
            OrganisationId = organisationId
        });
        db.Context.SaveChanges();
    }

    [Fact]
    public async Task SeedTestDataAsync_PopulatesCoreTables_WithoutError()
    {
        using var db = new SqliteTestContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id);

        var act = () => CreateSeeder(db).SeedTestDataAsync();
        await act.Should().NotThrowAsync();

        // Verify persistence through a fresh context (no shared change tracker), admin so
        // tenancy filters do not hide rows.
        await using var verify = db.NewContext(FakeCurrentUserService.Admin());

        (await verify.Parents.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.Children.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.TeamMembers.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.Activities.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.ActivityDays.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.ActivityGroups.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.ActivityQuestions.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.Bookings.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.BookingDays.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SeedTestDataAsync_PopulatesFinancialAndExcursionTables()
    {
        using var db = new SqliteTestContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id);

        await CreateSeeder(db).SeedTestDataAsync();

        await using var verify = db.NewContext(FakeCurrentUserService.Admin());

        (await verify.Payments.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.Expenses.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.Excursions.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.ExcursionRegistrations.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.ExcursionGroups.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        (await verify.ActivityFinancialTransactions.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
        // Sent-email history is seeded so the SentEmails view is not empty.
        (await verify.EmailsSent.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SeedTestDataAsync_PopulatesBankAccountAndEmailTemplates()
    {
        using var db = new SqliteTestContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id);

        await CreateSeeder(db).SeedTestDataAsync();

        await using var verify = db.NewContext(FakeCurrentUserService.Admin());

        // EnsureOrganisationBankAccountAsync filled the empty account.
        var seededOrg = await verify.Organisations.IgnoreQueryFilters().SingleAsync(o => o.Id == org.Id);
        seededOrg.BankAccountNumber.Should().NotBeNullOrEmpty();
        seededOrg.BankAccountName.Should().NotBeNullOrEmpty();

        // A user exists, so the email-template branch executed (4 templates per org).
        (await verify.EmailTemplates.IgnoreQueryFilters().CountAsync(t => t.OrganisationId == org.Id))
            .Should().Be(4);
    }

    [Fact]
    public async Task SeedTestDataAsync_GeneratesValidNationalRegisterNumbersAndStructuredCommunications()
    {
        using var db = new SqliteTestContext();
        var org = SeedOrganisation(db);

        await CreateSeeder(db).SeedTestDataAsync();

        await using var verify = db.NewContext(FakeCurrentUserService.Admin());

        // NRN format is YY.MM.DD-XXX.XX (the seeder formats with separators).
        var parent = await verify.Parents.IgnoreQueryFilters().FirstAsync();
        parent.NationalRegisterNumber.Should().MatchRegex(@"^\d{2}\.\d{2}\.\d{2}-\d{3}\.\d{2}$");

        // Bookings carry a structured communication in +++NNN/NNNN/NNNNN+++ form.
        var booking = await verify.Bookings.IgnoreQueryFilters()
            .FirstAsync(b => b.StructuredCommunication != null);
        booking.StructuredCommunication.Should().MatchRegex(@"^\+\+\+\d{3}/\d{4}/\d{5}\+\+\+$");
    }

    [Fact]
    public async Task SeedTestDataAsync_IsIdempotent_DoesNotDuplicateOnSecondRun()
    {
        using var db = new SqliteTestContext();
        var org = SeedOrganisation(db);
        SeedUser(db, org.Id);

        var seeder = CreateSeeder(db);
        await seeder.SeedTestDataAsync();

        int parentsAfterFirst;
        int templatesAfterFirst;
        await using (var verify1 = db.NewContext(FakeCurrentUserService.Admin()))
        {
            parentsAfterFirst = await verify1.Parents.IgnoreQueryFilters().CountAsync();
            templatesAfterFirst = await verify1.EmailTemplates.IgnoreQueryFilters().CountAsync();
        }

        // Second run over a fresh seeder/context on the same database must hit the
        // "already seeded" guards rather than duplicating data.
        await CreateSeeder(db).SeedTestDataAsync();

        await using var verify2 = db.NewContext(FakeCurrentUserService.Admin());
        (await verify2.Parents.IgnoreQueryFilters().CountAsync()).Should().Be(parentsAfterFirst);
        (await verify2.EmailTemplates.IgnoreQueryFilters().CountAsync()).Should().Be(templatesAfterFirst);
    }

    [Fact]
    public async Task SeedTestDataAsync_WithNoOrganisations_ReturnsWithoutSeeding()
    {
        using var db = new SqliteTestContext();

        // No organisation seeded -> the guard logs a warning and returns; nothing created, no throw.
        var act = () => CreateSeeder(db).SeedTestDataAsync();
        await act.Should().NotThrowAsync();

        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        (await verify.Parents.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await verify.Activities.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SeedTestDataAsync_AcrossMultipleOrganisations_ScopesDataPerTenant()
    {
        using var db = new SqliteTestContext();
        var org1 = SeedOrganisation(db, "Org A");
        var org2 = SeedOrganisation(db, "Org B");

        await CreateSeeder(db).SeedTestDataAsync();

        await using var verify = db.NewContext(FakeCurrentUserService.Admin());

        (await verify.Parents.IgnoreQueryFilters().CountAsync(p => p.OrganisationId == org1.Id))
            .Should().BeGreaterThan(0);
        (await verify.Parents.IgnoreQueryFilters().CountAsync(p => p.OrganisationId == org2.Id))
            .Should().BeGreaterThan(0);
        (await verify.Activities.IgnoreQueryFilters().CountAsync(a => a.OrganisationId == org1.Id))
            .Should().BeGreaterThan(0);
        (await verify.Activities.IgnoreQueryFilters().CountAsync(a => a.OrganisationId == org2.Id))
            .Should().BeGreaterThan(0);
    }
}
