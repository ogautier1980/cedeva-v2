using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Services.Email;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Services.Email;

/// <summary>
/// Covers the guarantee that every organisation gets a default email-template library, which is what
/// lets the app drop hard-coded HTML fallbacks.
/// </summary>
public class DefaultEmailTemplateLibraryTests
{
    private static (SqliteTestContext Db, int OrgId) NewDb()
    {
        var db = new SqliteTestContext();
        var org = TestData.Organisation("Org");
        using var seed = db.NewContext(FakeCurrentUserService.Admin());
        seed.Add(org);
        seed.SaveChanges();
        return (db, org.Id);
    }

    [Fact]
    public async Task EnsureAsync_CreatesOrgLevelLibrary_WithBookingConfirmationDefault()
    {
        var (db, orgId) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Admin());

        var created = await DefaultEmailTemplateLibrary.EnsureAsync(ctx, orgId);

        created.Should().Be(7);
        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        var templates = await verify.EmailTemplates.IgnoreQueryFilters()
            .Where(t => t.OrganisationId == orgId && t.ActivityId == null).ToListAsync();
        templates.Should().HaveCount(7);
        templates.Should().ContainSingle(t => t.TemplateType == EmailTemplateType.BookingConfirmation && t.IsDefault);
        templates.Should().ContainSingle(t => t.TemplateType == EmailTemplateType.ExcursionProposal && t.IsDefault);
    }

    [Fact]
    public async Task EnsureAsync_IsIdempotent()
    {
        var (db, orgId) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Admin());

        await DefaultEmailTemplateLibrary.EnsureAsync(ctx, orgId);
        var secondRun = await DefaultEmailTemplateLibrary.EnsureAsync(ctx, orgId);

        secondRun.Should().Be(0, "the library already exists");
        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        (await verify.EmailTemplates.IgnoreQueryFilters().CountAsync(t => t.OrganisationId == orgId)).Should().Be(7);
    }

    [Fact]
    public async Task EnsureAsync_BackfillsOnlyMissingTypes_WhenSomeTemplatesAlreadyExist()
    {
        // Regression test for the per-type backfill (needed so a newly added default template type
        // reaches pre-existing organisations too — DbSeeder calls EnsureAsync on every startup).
        var (db, orgId) = NewDb();
        using var _d = db;
        using (var seed = db.NewContext(FakeCurrentUserService.Admin()))
        {
            seed.EmailTemplates.Add(new EmailTemplate
            {
                OrganisationId = orgId, ActivityId = null, TemplateType = EmailTemplateType.Custom,
                Name = "Custom", Subject = "s", HtmlContent = "<p>x</p>", IsDefault = true
            });
            seed.SaveChanges();
        }

        using var ctx = db.NewContext(FakeCurrentUserService.Admin());
        var created = await DefaultEmailTemplateLibrary.EnsureAsync(ctx, orgId);

        created.Should().Be(6, "Custom already existed — the other 6 default types should still be backfilled");
        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        (await verify.EmailTemplates.IgnoreQueryFilters().CountAsync(t => t.OrganisationId == orgId)).Should().Be(7);
        (await verify.EmailTemplates.IgnoreQueryFilters()
            .CountAsync(t => t.OrganisationId == orgId && t.TemplateType == EmailTemplateType.Custom)).Should().Be(1,
            "the pre-existing Custom template is left untouched, not duplicated");
    }
}
