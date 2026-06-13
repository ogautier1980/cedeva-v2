using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cedeva.Tests.Data;

/// <summary>
/// Smoke / integration coverage for <see cref="DbSeeder"/> run against the REAL application
/// schema (the same EF Core model the production app uses), booted through
/// <see cref="CedevaWebApplicationFactory"/> so the genuine ASP.NET Identity
/// <see cref="UserManager{TUser}"/> / <see cref="RoleManager{TRole}"/> and the registered
/// <see cref="CedevaDbContext"/> are exercised end to end.
///
/// <para><b>Why the private steps are invoked directly.</b> <see cref="DbSeeder.SeedAsync"/>'s
/// first statement is <c>_context.Database.MigrateAsync()</c>. The committed migrations are
/// SQL-Server-specific (e.g. <c>nvarchar(max)</c>, <c>SqlServer:Identity</c>) and the SQLite
/// migration SQL generator emits invalid DDL for them (<c>SQLite Error 1: near "max"</c>), so a
/// full <c>SeedAsync()</c> can never reach the seeding body on the SQLite test database — it
/// always fails at the migrate step. That failure path (the try/catch wrapper) is asserted in
/// <see cref="SeedAsync_OnSqliteSchema_WrapsMigrateFailure"/>. To cover the actual seeding logic
/// (roles, admin user, demo organisations + coordinators, Belgian municipalities, and their
/// idempotency guards) the schema is created with <c>EnsureCreated()</c> — which honours the live
/// model rather than the SQL-Server migrations — and the four seeding steps are driven through
/// reflection. We may not modify <c>src</c> to make these steps public, so reflection is the only
/// way to reach the real code with the real Identity stack.</para>
/// </summary>
[Collection("WebApp")]
public sealed class DbSeederSmokeTests : IDisposable
{
    private readonly CedevaWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    private readonly CedevaDbContext _context;
    private readonly UserManager<CedevaUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly DbSeeder _seeder;
    private readonly string _csvPath = Path.Combine(AppContext.BaseDirectory, "municipalities.csv");

    public DbSeederSmokeTests()
    {
        _factory = new CedevaWebApplicationFactory();
        // Creates the SQLite schema from the live model (works; migrations do not on SQLite).
        _factory.Seed(_ => 0);

        _scope = _factory.Services.CreateScope();
        var sp = _scope.ServiceProvider;
        _context = sp.GetRequiredService<CedevaDbContext>();
        _userManager = sp.GetRequiredService<UserManager<CedevaUser>>();
        _roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        // Real DbSeeder over the real Identity managers + real DbContext.
        _seeder = new DbSeeder(_context, _userManager, _roleManager, NullLogger<DbSeeder>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_csvPath)) File.Delete(_csvPath);
        _scope.Dispose();
        _factory.Dispose();
    }

    // ---- helpers ----------------------------------------------------------

    private static Task Invoke(DbSeeder seeder, string method)
    {
        var m = typeof(DbSeeder).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(DbSeeder), method);
        return (Task)m.Invoke(seeder, null)!;
    }

    private Task SeedRoles() => Invoke(_seeder, "SeedRolesAsync");
    private Task SeedMunicipalities() => Invoke(_seeder, "SeedBelgianMunicipalitiesAsync");
    private Task SeedAdmin() => Invoke(_seeder, "SeedAdminUserAsync");
    private Task SeedDemoOrgs() => Invoke(_seeder, "SeedDemoOrganisationAsync");

    private void WriteCsv(string content) => File.WriteAllText(_csvPath, content);

    private const string ValidCsv = "1000;Bruxelles\n5030;Gembloux\n4000;Liège\n";

    // ---- roles ------------------------------------------------------------

    [Fact]
    public async Task SeedRoles_CreatesAdminAndCoordinatorRoles()
    {
        await SeedRoles();

        var names = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        names.Should().Contain("Admin");
        names.Should().Contain("Coordinator");
        names.Should().HaveCount(2);
    }

    [Fact]
    public async Task SeedRoles_IsIdempotent_NoDuplicateRolesOnSecondRun()
    {
        await SeedRoles();
        await SeedRoles();

        (await _roleManager.Roles.CountAsync()).Should().Be(2);
    }

    // ---- admin user -------------------------------------------------------

    [Fact]
    public async Task SeedAdminUser_CreatesAdminWithRoleAndConfirmedEmail()
    {
        await SeedRoles();
        await SeedAdmin();

        var admin = await _userManager.FindByEmailAsync("admin@cedeva.be");
        admin.Should().NotBeNull();
        admin!.EmailConfirmed.Should().BeTrue();
        admin.Role.Should().Be(Role.Admin);
        admin.OrganisationId.Should().BeNull();
        (await _userManager.IsInRoleAsync(admin, "Admin")).Should().BeTrue();
    }

    [Fact]
    public async Task SeedAdminUser_IsIdempotent_DoesNotCreateSecondAdmin()
    {
        await SeedRoles();
        await SeedAdmin();
        await SeedAdmin();

        (await _userManager.Users.CountAsync(u => u.Email == "admin@cedeva.be")).Should().Be(1);
    }

    // ---- demo organisations + coordinators --------------------------------

    [Fact]
    public async Task SeedDemoOrganisations_CreatesTwoOrgsEachWithCoordinator()
    {
        await SeedRoles();
        await SeedDemoOrgs();

        var orgs = await _context.Organisations.IgnoreQueryFilters().ToListAsync();
        orgs.Should().HaveCount(2);
        orgs.Select(o => o.Name).Should().Contain("Plaine de Bossière");
        orgs.Select(o => o.Name).Should().Contain("Centre Récréatif Les Aventuriers");

        var coord1 = await _userManager.FindByEmailAsync("coordinator@cedeva.be");
        var coord2 = await _userManager.FindByEmailAsync("coordinator.liege@cedeva.be");
        coord1.Should().NotBeNull();
        coord2.Should().NotBeNull();
        coord1!.Role.Should().Be(Role.Coordinator);
        coord2!.Role.Should().Be(Role.Coordinator);
        coord1.OrganisationId.Should().NotBeNull();
        coord2.OrganisationId.Should().NotBeNull();
        (await _userManager.IsInRoleAsync(coord1, "Coordinator")).Should().BeTrue();
        (await _userManager.IsInRoleAsync(coord2, "Coordinator")).Should().BeTrue();
    }

    [Fact]
    public async Task SeedDemoOrganisations_WiresCoordinatorsToTheirOwnOrganisation()
    {
        await SeedRoles();
        await SeedDemoOrgs();

        var bossiere = await _context.Organisations.IgnoreQueryFilters()
            .SingleAsync(o => o.Name == "Plaine de Bossière");
        var coord1 = await _userManager.FindByEmailAsync("coordinator@cedeva.be");

        coord1!.OrganisationId.Should().Be(bossiere.Id);
    }

    [Fact]
    public async Task SeedDemoOrganisations_IsIdempotent_DoesNotExceedTwoOrgs()
    {
        await SeedRoles();
        await SeedDemoOrgs();
        await SeedDemoOrgs();

        (await _context.Organisations.IgnoreQueryFilters().CountAsync()).Should().Be(2);
        (await _userManager.Users.CountAsync(u => u.Email == "coordinator@cedeva.be")).Should().Be(1);
        (await _userManager.Users.CountAsync(u => u.Email == "coordinator.liege@cedeva.be")).Should().Be(1);
    }

    [Fact]
    public async Task SeedDemoOrganisations_WithOneExistingOrg_AddsOnlyTheSecond()
    {
        // Pre-seed a single organisation so the "existingCount == 1" branch runs:
        // the first-org block is skipped, only the second org + its coordinator are created.
        _context.Organisations.Add(new Organisation
        {
            Name = "Existing Org",
            Description = "Pre-seeded",
            Address = new Address
            {
                Street = "Rue X 1",
                City = "Namur",
                PostalCode = "5000",
                Country = Country.Belgium
            }
        });
        await _context.SaveChangesAsync();

        await SeedRoles();
        await SeedDemoOrgs();

        (await _context.Organisations.IgnoreQueryFilters().CountAsync()).Should().Be(2);
        // First demo org/coordinator NOT created; only the second one was added.
        (await _userManager.FindByEmailAsync("coordinator@cedeva.be")).Should().BeNull();
        (await _userManager.FindByEmailAsync("coordinator.liege@cedeva.be")).Should().NotBeNull();
    }

    // ---- Belgian municipalities -------------------------------------------

    [Fact]
    public async Task SeedMunicipalities_ParsesCsv_SkippingBlankAndMalformedLines()
    {
        // Blank line, a single-column malformed line and a 3-column line must all be skipped.
        WriteCsv("1000;Bruxelles\n\n5030;Gembloux\nmalformed\n4000;Liège;extra\n7000;Mons\n");

        await SeedMunicipalities();

        var munis = await _context.BelgianMunicipalities.ToListAsync();
        munis.Should().HaveCount(3); // Bruxelles, Gembloux, Mons
        munis.Select(m => m.PostalCode).Should().Contain("1000");
        munis.Single(m => m.PostalCode == "5030").City.Should().Be("Gembloux");
    }

    [Fact]
    public async Task SeedMunicipalities_IsRerunnable_ReplacesExistingRowsInsteadOfDuplicating()
    {
        WriteCsv(ValidCsv);
        await SeedMunicipalities();
        (await _context.BelgianMunicipalities.CountAsync()).Should().Be(3);

        // Second run must hit the "remove existing then re-add" branch — no duplication.
        WriteCsv("1000;Bruxelles\n5030;Gembloux\n");
        await SeedMunicipalities();

        (await _context.BelgianMunicipalities.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SeedMunicipalities_WhenCsvMissing_ThrowsFileNotFound()
    {
        if (File.Exists(_csvPath)) File.Delete(_csvPath);

        var act = () => SeedMunicipalities();

        // The exception is raised inside the awaited Task (not at reflection-invoke time), so it
        // surfaces unwrapped rather than as a TargetInvocationException.
        var ex = await act.Should().ThrowAsync<FileNotFoundException>();
        ex.Which.Message.Should().Contain("municipalities.csv");
    }

    // ---- full pipeline (private steps in production order) -----------------

    [Fact]
    public async Task AllSeedingSteps_InOrder_ProduceCompleteDemoDataSet()
    {
        WriteCsv(ValidCsv);

        await SeedRoles();
        await SeedMunicipalities();
        await SeedAdmin();
        await SeedDemoOrgs();

        (await _roleManager.Roles.CountAsync()).Should().Be(2);
        (await _userManager.Users.CountAsync()).Should().Be(3); // admin + 2 coordinators
        (await _context.Organisations.IgnoreQueryFilters().CountAsync()).Should().Be(2);
        (await _context.BelgianMunicipalities.CountAsync()).Should().Be(3);
    }

    // ---- public SeedAsync entry point (migrate-failure wrapper) ------------

    [Fact]
    public async Task SeedAsync_OnSqliteSchema_WrapsMigrateFailure()
    {
        // SeedAsync() starts with MigrateAsync(); the SQL-Server migrations cannot apply to
        // SQLite, so the seeder catches the failure and rethrows it as InvalidOperationException
        // ("Database seeding failed"). This asserts the public entry point's try/catch wrapper.
        var act = () => _seeder.SeedAsync();

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("Database seeding failed");
        ex.Which.InnerException.Should().NotBeNull();
    }
}
