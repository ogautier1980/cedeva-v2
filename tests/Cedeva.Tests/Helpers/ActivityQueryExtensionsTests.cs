using Cedeva.Tests.TestSupport;
using Cedeva.Website.Infrastructure;

namespace Cedeva.Tests.Helpers;

/// <summary>
/// The activity-name uniqueness check shared by ActivitiesController (Create/Edit) and the wizard.
/// Names are ASCII on purpose: SQLite's lower() only folds ASCII letters ("É" stays "É"), whereas
/// PostgreSQL's lower() folds accented capitals too (checked on postgres:17-alpine, en_US.utf8).
/// </summary>
public class ActivityQueryExtensionsTests
{
    /// <summary>Seeds "Stage Plaine" in Org1 and returns (org1 id, org2 id, activity id).</summary>
    private static (int Org1Id, int Org2Id, int ActivityId) Seed(SqliteTestContext db)
    {
        var org1 = TestData.Organisation("Org1");
        var org2 = TestData.Organisation("Org2");
        var activity = TestData.Activity(org1, "Stage Plaine");
        db.Context.AddRange(org1, org2, activity);
        db.Context.SaveChanges();
        return (org1.Id, org2.Id, activity.Id);
    }

    [Theory]
    [InlineData("Stage Plaine")]
    [InlineData("stage plaine")]
    [InlineData("  STAGE PLAINE  ")]
    public async Task NameExistsInOrganisationAsync_SameNameIgnoringCaseAndSpaces_IsFound(string name)
    {
        using var db = new SqliteTestContext();
        var (org1Id, _, _) = Seed(db);

        (await db.NewContext().Activities.NameExistsInOrganisationAsync(org1Id, name)).Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsInOrganisationAsync_SameNameInAnotherOrganisation_IsNotFound()
    {
        using var db = new SqliteTestContext();
        var (_, org2Id, _) = Seed(db);

        (await db.NewContext().Activities.NameExistsInOrganisationAsync(org2Id, "Stage Plaine")).Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsInOrganisationAsync_ExcludedActivity_IsNotFound()
    {
        using var db = new SqliteTestContext();
        var (org1Id, _, activityId) = Seed(db);

        // Editing an activity without renaming it must not report a clash with itself.
        (await db.NewContext().Activities.NameExistsInOrganisationAsync(org1Id, "Stage Plaine", activityId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsInOrganisationAsync_BypassesTheTenancyFilter()
    {
        using var db = new SqliteTestContext();
        var (org1Id, org2Id, _) = Seed(db);

        // A context scoped to Org2 still sees Org1's activities when checking Org1.
        await using var org2Ctx = db.NewContext(FakeCurrentUserService.Coordinator(org2Id));
        (await org2Ctx.Activities.NameExistsInOrganisationAsync(org1Id, "Stage Plaine")).Should().BeTrue();
    }
}
