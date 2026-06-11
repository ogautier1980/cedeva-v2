using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Data;

/// <summary>
/// Verifies the EF Core global query filters that enforce organisation-scoped multi-tenancy.
/// </summary>
public class MultiTenancyQueryFilterTests
{
    /// <summary>Seeds one activity in each of two organisations; returns org1's id.</summary>
    private static int SeedTwoOrganisations(SqliteTestContext db)
    {
        var org1 = TestData.Organisation("Org1");
        var org2 = TestData.Organisation("Org2");
        var a1 = TestData.Activity(org1, "Activity-Org1");
        var a2 = TestData.Activity(org2, "Activity-Org2");
        db.Context.AddRange(org1, org2, a1, a2);
        db.Context.SaveChanges();
        return org1.Id;
    }

    [Fact]
    public async Task Coordinator_SeesOnlyOwnOrganisation()
    {
        using var db = new SqliteTestContext(); // admin context used for seeding
        var org1Id = SeedTwoOrganisations(db);

        await using var coordinatorCtx = db.NewContext(FakeCurrentUserService.Coordinator(org1Id));
        var activities = await coordinatorCtx.Activities.ToListAsync();

        activities.Should().ContainSingle()
            .Which.Name.Should().Be("Activity-Org1");
    }

    [Fact]
    public async Task Admin_SeesAllOrganisations()
    {
        using var db = new SqliteTestContext();
        SeedTwoOrganisations(db);

        await using var adminCtx = db.NewContext(FakeCurrentUserService.Admin());
        var activities = await adminCtx.Activities.ToListAsync();

        activities.Should().HaveCount(2);
    }

    [Fact]
    public async Task IgnoreQueryFilters_BypassesTenantScopeForCoordinator()
    {
        using var db = new SqliteTestContext();
        var org1Id = SeedTwoOrganisations(db);

        await using var coordinatorCtx = db.NewContext(FakeCurrentUserService.Coordinator(org1Id));
        var all = await coordinatorCtx.Activities.IgnoreQueryFilters().ToListAsync();

        all.Should().HaveCount(2);
    }
}
