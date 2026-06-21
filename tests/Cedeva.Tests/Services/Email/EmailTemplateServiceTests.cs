using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Services.Email;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Services.Email;

public class EmailTemplateServiceTests
{
    /// <summary>
    /// Seeds two organisations (EmailTemplate.OrganisationId is a required FK) and returns the
    /// test context plus the generated organisation ids. OrgA is the "current" tenant.
    /// </summary>
    private static (SqliteTestContext Db, int OrgA, int OrgB) NewDb(ICurrentUserService? user = null)
    {
        var db = new SqliteTestContext(user);
        var orgA = TestData.Organisation("Org A");
        var orgB = TestData.Organisation("Org B");
        // Seed orgs via an admin context so tenant filters never hide them during setup.
        using var seed = db.NewContext(FakeCurrentUserService.Admin());
        seed.AddRange(orgA, orgB);
        seed.SaveChanges();
        return (db, orgA.Id, orgB.Id);
    }

    private static EmailTemplate Template(
        int organisationId,
        EmailTemplateType type,
        string name,
        bool isDefault = false,
        int? activityId = null) => new()
    {
        OrganisationId = organisationId,
        TemplateType = type,
        Name = name,
        Subject = "Subject " + name,
        HtmlContent = "<p>" + name + "</p>",
        IsDefault = isDefault,
        ActivityId = activityId
    };

    /// <summary>Adds an activity (FK target for activity-scoped templates) and returns its id.</summary>
    private static int SeedActivity(SqliteTestContext db, int organisationId, string name = "Stage")
    {
        using var seed = db.NewContext(FakeCurrentUserService.Admin());
        var activity = new Activity
        {
            Name = name,
            Description = "x",
            IsActive = true,
            PricePerDay = 20m,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 5),
            OrganisationId = organisationId
        };
        seed.Activities.Add(activity);
        seed.SaveChanges();
        return activity.Id;
    }

    /// <summary>Adds templates via an admin context (bypasses tenant filter) over the same db.</summary>
    private static void SeedTemplates(SqliteTestContext db, params EmailTemplate[] templates)
    {
        using var seed = db.NewContext(FakeCurrentUserService.Admin());
        seed.EmailTemplates.AddRange(templates);
        seed.SaveChanges();
    }

    // ----- GetDefaultTemplateAsync -----------------------------------------

    [Fact]
    public async Task GetDefaultTemplate_ReturnsTheDefaultForTypeAndOrg()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        SeedTemplates(db,
            Template(orgA, EmailTemplateType.BookingConfirmation, "Default", isDefault: true),
            Template(orgA, EmailTemplateType.BookingConfirmation, "Other", isDefault: false));
        var sut = new EmailTemplateService(db.Context);

        var result = await sut.GetDefaultTemplateAsync(EmailTemplateType.BookingConfirmation, orgA);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Default");
    }

    [Fact]
    public async Task GetDefaultTemplate_NoDefault_ReturnsNull()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        SeedTemplates(db, Template(orgA, EmailTemplateType.BookingConfirmation, "NotDefault", isDefault: false));
        var sut = new EmailTemplateService(db.Context);

        (await sut.GetDefaultTemplateAsync(EmailTemplateType.BookingConfirmation, orgA))
            .Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultTemplate_DifferentType_ReturnsNull()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        SeedTemplates(db, Template(orgA, EmailTemplateType.BookingConfirmation, "Default", isDefault: true));
        var sut = new EmailTemplateService(db.Context);

        (await sut.GetDefaultTemplateAsync(EmailTemplateType.PaymentReminder, orgA))
            .Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultTemplate_DifferentOrg_ReturnsNull()
    {
        var (db, orgA, orgB) = NewDb();
        using var _d = db;
        SeedTemplates(db, Template(orgA, EmailTemplateType.BookingConfirmation, "Default", isDefault: true));
        var sut = new EmailTemplateService(db.Context);

        (await sut.GetDefaultTemplateAsync(EmailTemplateType.BookingConfirmation, orgB))
            .Should().BeNull();
    }

    // ----- GetTemplatesByTypeAsync -----------------------------------------

    [Fact]
    public async Task GetTemplatesByType_FiltersByTypeAndOrg_DefaultFirstThenByName()
    {
        var (db, orgA, orgB) = NewDb();
        using var _d = db;
        SeedTemplates(db,
            Template(orgA, EmailTemplateType.BookingConfirmation, "Zeta", isDefault: false),
            Template(orgA, EmailTemplateType.BookingConfirmation, "Alpha", isDefault: false),
            Template(orgA, EmailTemplateType.BookingConfirmation, "TheDefault", isDefault: true),
            Template(orgA, EmailTemplateType.PaymentReminder, "WrongType"),
            Template(orgB, EmailTemplateType.BookingConfirmation, "WrongOrg"));
        var sut = new EmailTemplateService(db.Context);

        var result = await sut.GetTemplatesByTypeAsync(EmailTemplateType.BookingConfirmation, orgA);

        result.Should().HaveCount(3);
        result.Select(t => t.Name).Should().ContainInOrder("TheDefault", "Alpha", "Zeta");
    }

    [Fact]
    public async Task GetTemplatesByType_NoMatches_ReturnsEmpty()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var sut = new EmailTemplateService(db.Context);

        (await sut.GetTemplatesByTypeAsync(EmailTemplateType.Custom, orgA)).Should().BeEmpty();
    }

    // ----- GetAllTemplatesAsync --------------------------------------------

    [Fact]
    public async Task GetAllTemplates_FiltersByOrg_OrderedByTypeThenDefaultThenName()
    {
        var (db, orgA, orgB) = NewDb();
        using var _d = db;
        SeedTemplates(db,
            Template(orgA, EmailTemplateType.PaymentReminder, "PR-A"),
            Template(orgA, EmailTemplateType.BookingConfirmation, "BC-Zeta"),
            Template(orgA, EmailTemplateType.BookingConfirmation, "BC-Default", isDefault: true),
            Template(orgB, EmailTemplateType.BookingConfirmation, "WrongOrg"));
        var sut = new EmailTemplateService(db.Context);

        var result = await sut.GetAllTemplatesAsync(orgA);

        result.Should().HaveCount(3);
        // BookingConfirmation (type 1) before PaymentReminder (type 4); within type, default first
        result.Select(t => t.Name).Should().ContainInOrder("BC-Default", "BC-Zeta", "PR-A");
    }

    [Fact]
    public async Task GetAllTemplates_OtherOrgOnly_ReturnsEmpty()
    {
        var (db, orgA, orgB) = NewDb();
        using var _d = db;
        SeedTemplates(db, Template(orgB, EmailTemplateType.Custom, "OtherOrg"));
        var sut = new EmailTemplateService(db.Context);

        (await sut.GetAllTemplatesAsync(orgA)).Should().BeEmpty();
    }

    // ----- GetTemplateByIdAsync --------------------------------------------

    [Fact]
    public async Task GetTemplateById_Existing_ReturnsTemplate()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var template = Template(orgA, EmailTemplateType.Custom, "ById");
        SeedTemplates(db, template);
        var sut = new EmailTemplateService(db.Context);

        var result = await sut.GetTemplateByIdAsync(template.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("ById");
    }

    [Fact]
    public async Task GetTemplateById_Unknown_ReturnsNull()
    {
        var (db, _, _) = NewDb();
        using var _d = db;
        var sut = new EmailTemplateService(db.Context);

        (await sut.GetTemplateByIdAsync(99999)).Should().BeNull();
    }

    // ----- CreateTemplateAsync ---------------------------------------------

    [Fact]
    public async Task Create_PersistsTemplateAndSetsCreatedAt()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var sut = new EmailTemplateService(db.Context);

        var created = await sut.CreateTemplateAsync(
            Template(orgA, EmailTemplateType.Custom, "New"));

        created.Id.Should().BeGreaterThan(0);
        created.CreatedAt.Should().NotBe(default);

        await using var verify = db.NewContext();
        var persisted = await verify.EmailTemplates.SingleAsync(t => t.Id == created.Id);
        persisted.Name.Should().Be("New");
    }

    [Fact]
    public async Task Create_AsDefault_UnsetsExistingDefaultOfSameTypeAndOrg()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        SeedTemplates(db, Template(orgA, EmailTemplateType.BookingConfirmation, "OldDefault", isDefault: true));
        var sut = new EmailTemplateService(db.Context);

        await sut.CreateTemplateAsync(
            Template(orgA, EmailTemplateType.BookingConfirmation, "NewDefault", isDefault: true));

        await using var verify = db.NewContext();
        var templates = await verify.EmailTemplates
            .Where(t => t.OrganisationId == orgA && t.TemplateType == EmailTemplateType.BookingConfirmation)
            .ToListAsync();
        templates.Should().HaveCount(2);
        templates.Where(t => t.IsDefault).Should().ContainSingle()
            .Which.Name.Should().Be("NewDefault");
    }

    [Fact]
    public async Task Create_AsDefault_DoesNotTouchDefaultsOfOtherTypesOrOrgs()
    {
        var (db, orgA, orgB) = NewDb();
        using var _d = db;
        var otherType = Template(orgA, EmailTemplateType.PaymentReminder, "OtherTypeDefault", isDefault: true);
        var otherOrg = Template(orgB, EmailTemplateType.BookingConfirmation, "OtherOrgDefault", isDefault: true);
        SeedTemplates(db, otherType, otherOrg);
        var sut = new EmailTemplateService(db.Context);

        await sut.CreateTemplateAsync(
            Template(orgA, EmailTemplateType.BookingConfirmation, "NewDefault", isDefault: true));

        await using var verify = db.NewContext();
        (await verify.EmailTemplates.IgnoreQueryFilters().SingleAsync(t => t.Id == otherType.Id))
            .IsDefault.Should().BeTrue();
        (await verify.EmailTemplates.IgnoreQueryFilters().SingleAsync(t => t.Id == otherOrg.Id))
            .IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Create_NotDefault_LeavesExistingDefaultUntouched()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var existing = Template(orgA, EmailTemplateType.BookingConfirmation, "Default", isDefault: true);
        SeedTemplates(db, existing);
        var sut = new EmailTemplateService(db.Context);

        await sut.CreateTemplateAsync(
            Template(orgA, EmailTemplateType.BookingConfirmation, "Extra", isDefault: false));

        await using var verify = db.NewContext();
        (await verify.EmailTemplates.SingleAsync(t => t.Id == existing.Id))
            .IsDefault.Should().BeTrue();
    }

    // ----- UpdateTemplateAsync ---------------------------------------------

    [Fact]
    public async Task Update_PersistsChangesAndSetsModifiedAt()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var template = Template(orgA, EmailTemplateType.Custom, "Original");
        SeedTemplates(db, template);

        await using var editCtx = db.NewContext();
        var sut = new EmailTemplateService(editCtx);
        var toUpdate = await editCtx.EmailTemplates.SingleAsync(t => t.Id == template.Id);
        toUpdate.Name = "Updated";
        await sut.UpdateTemplateAsync(toUpdate);

        await using var verify = db.NewContext();
        var persisted = await verify.EmailTemplates.SingleAsync(t => t.Id == template.Id);
        persisted.Name.Should().Be("Updated");
        persisted.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_SetAsDefault_UnsetsPreviousDefaultButKeepsItself()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var oldDefault = Template(orgA, EmailTemplateType.BookingConfirmation, "OldDefault", isDefault: true);
        var promoted = Template(orgA, EmailTemplateType.BookingConfirmation, "Promoted", isDefault: false);
        SeedTemplates(db, oldDefault, promoted);

        await using var editCtx = db.NewContext();
        var sut = new EmailTemplateService(editCtx);
        var toUpdate = await editCtx.EmailTemplates.SingleAsync(t => t.Id == promoted.Id);
        toUpdate.IsDefault = true;
        await sut.UpdateTemplateAsync(toUpdate);

        await using var verify = db.NewContext();
        (await verify.EmailTemplates.SingleAsync(t => t.Id == oldDefault.Id))
            .IsDefault.Should().BeFalse();
        (await verify.EmailTemplates.SingleAsync(t => t.Id == promoted.Id))
            .IsDefault.Should().BeTrue();
    }

    // ----- DeleteTemplateAsync ---------------------------------------------

    [Fact]
    public async Task Delete_Existing_RemovesTemplate()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var template = Template(orgA, EmailTemplateType.Custom, "ToDelete");
        SeedTemplates(db, template);
        var sut = new EmailTemplateService(db.Context);

        await sut.DeleteTemplateAsync(template.Id);

        await using var verify = db.NewContext();
        (await verify.EmailTemplates.AnyAsync(t => t.Id == template.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Unknown_DoesNothing()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        SeedTemplates(db, Template(orgA, EmailTemplateType.Custom, "Keep"));
        var sut = new EmailTemplateService(db.Context);

        var act = async () => await sut.DeleteTemplateAsync(99999);

        await act.Should().NotThrowAsync();
        await using var verify = db.NewContext();
        (await verify.EmailTemplates.CountAsync()).Should().Be(1);
    }

    // ----- SetDefaultTemplateAsync -----------------------------------------

    [Fact]
    public async Task SetDefault_PromotesTargetAndUnsetsPreviousDefault()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var oldDefault = Template(orgA, EmailTemplateType.BookingConfirmation, "OldDefault", isDefault: true);
        var target = Template(orgA, EmailTemplateType.BookingConfirmation, "Target", isDefault: false);
        SeedTemplates(db, oldDefault, target);
        var sut = new EmailTemplateService(db.Context);

        await sut.SetDefaultTemplateAsync(target.Id, EmailTemplateType.BookingConfirmation);

        await using var verify = db.NewContext();
        (await verify.EmailTemplates.SingleAsync(t => t.Id == target.Id)).IsDefault.Should().BeTrue();
        (await verify.EmailTemplates.SingleAsync(t => t.Id == oldDefault.Id)).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefault_UnknownTemplate_Throws()
    {
        var (db, _, _) = NewDb();
        using var _d = db;
        var sut = new EmailTemplateService(db.Context);

        var act = async () => await sut.SetDefaultTemplateAsync(99999, EmailTemplateType.Custom);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ----- Multi-tenancy ----------------------------------------------------

    [Fact]
    public async Task GetAllTemplates_AsCoordinator_OnlySeesOwnOrg()
    {
        // Seed with admin so both orgs and templates are created, then act as a coordinator of OrgA.
        var (db, orgA, orgB) = NewDb();
        using var _d = db;
        SeedTemplates(db,
            Template(orgA, EmailTemplateType.Custom, "MineA"),
            Template(orgA, EmailTemplateType.Custom, "MineB"),
            Template(orgB, EmailTemplateType.Custom, "OtherOrg"));

        await using var coordCtx = db.NewContext(FakeCurrentUserService.Coordinator(orgA));
        var sut = new EmailTemplateService(coordCtx);

        var mine = await sut.GetAllTemplatesAsync(orgA);
        var other = await sut.GetAllTemplatesAsync(orgB);

        mine.Select(t => t.Name).Should().BeEquivalentTo(new[] { "MineA", "MineB" });
        // Even when explicitly asking for OrgB, the coordinator's query filter hides it.
        other.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTemplateById_OtherOrg_AsCoordinator_ReturnsNull()
    {
        var (db, orgA, orgB) = NewDb();
        using var _d = db;
        var other = Template(orgB, EmailTemplateType.Custom, "OtherOrg");
        SeedTemplates(db, other);

        await using var coordCtx = db.NewContext(FakeCurrentUserService.Coordinator(orgA));
        var sut = new EmailTemplateService(coordCtx);

        (await sut.GetTemplateByIdAsync(other.Id)).Should().BeNull();
    }

    // ----- Activity scope: defaults, fallback, listing -----------------------

    [Fact]
    public async Task GetDefaultTemplate_WithActivity_PrefersActivityDefault()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var activityId = SeedActivity(db, orgA);
        SeedTemplates(db,
            Template(orgA, EmailTemplateType.BookingConfirmation, "OrgDefault", isDefault: true),
            Template(orgA, EmailTemplateType.BookingConfirmation, "ActivityDefault", isDefault: true, activityId: activityId));
        var sut = new EmailTemplateService(db.Context);

        var result = await sut.GetDefaultTemplateAsync(EmailTemplateType.BookingConfirmation, orgA, activityId);

        result!.Name.Should().Be("ActivityDefault");
    }

    [Fact]
    public async Task GetDefaultTemplate_WithActivity_FallsBackToOrgDefault()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var activityId = SeedActivity(db, orgA);
        SeedTemplates(db, Template(orgA, EmailTemplateType.BookingConfirmation, "OrgDefault", isDefault: true));
        var sut = new EmailTemplateService(db.Context);

        var result = await sut.GetDefaultTemplateAsync(EmailTemplateType.BookingConfirmation, orgA, activityId);

        result!.Name.Should().Be("OrgDefault", "no activity default exists, so the org default applies");
    }

    [Fact]
    public async Task GetAllTemplates_ScopeSeparatesOrgLevelFromActivityLevel()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var activityId = SeedActivity(db, orgA);
        SeedTemplates(db,
            Template(orgA, EmailTemplateType.Custom, "OrgLevel"),
            Template(orgA, EmailTemplateType.Custom, "ActivityLevel", activityId: activityId));
        var sut = new EmailTemplateService(db.Context);

        (await sut.GetAllTemplatesAsync(orgA)).Select(t => t.Name).Should().BeEquivalentTo(new[] { "OrgLevel" });
        (await sut.GetAllTemplatesAsync(orgA, activityId)).Select(t => t.Name).Should().BeEquivalentTo(new[] { "ActivityLevel" });
    }

    // ----- Mandatory default per (scope, type) ------------------------------

    [Fact]
    public async Task Create_FirstOfTypeInScope_BecomesDefaultEvenIfNotRequested()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var sut = new EmailTemplateService(db.Context);

        var created = await sut.CreateTemplateAsync(
            Template(orgA, EmailTemplateType.PaymentReminder, "First", isDefault: false));

        created.IsDefault.Should().BeTrue("the first template of a type in a scope is the mandatory default");
    }

    [Fact]
    public async Task Delete_Default_PromotesAnotherTemplateOfSameType()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var theDefault = Template(orgA, EmailTemplateType.BookingConfirmation, "Default", isDefault: true);
        var other = Template(orgA, EmailTemplateType.BookingConfirmation, "Other", isDefault: false);
        SeedTemplates(db, theDefault, other);
        var sut = new EmailTemplateService(db.Context);

        await sut.DeleteTemplateAsync(theDefault.Id);

        await using var verify = db.NewContext();
        (await verify.EmailTemplates.SingleAsync(t => t.Id == other.Id))
            .IsDefault.Should().BeTrue("deleting the default promotes the remaining template");
    }

    // ----- Copy / import -----------------------------------------------------

    [Fact]
    public async Task CopyOrganisationTemplatesToActivity_CopiesLibrary_SkippingExistingTypes()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var activityId = SeedActivity(db, orgA);
        SeedTemplates(db,
            Template(orgA, EmailTemplateType.BookingConfirmation, "OrgBC", isDefault: true),
            Template(orgA, EmailTemplateType.PaymentReminder, "OrgPR", isDefault: true),
            // Activity already has a BookingConfirmation -> that type must be skipped.
            Template(orgA, EmailTemplateType.BookingConfirmation, "ExistingBC", isDefault: true, activityId: activityId));
        var sut = new EmailTemplateService(db.Context);

        var created = await sut.CopyOrganisationTemplatesToActivityAsync(orgA, activityId);

        created.Should().Be(1, "only PaymentReminder is missing on the activity");
        var activityTemplates = await sut.GetAllTemplatesAsync(orgA, activityId);
        activityTemplates.Select(t => t.Name).Should().BeEquivalentTo(new[] { "ExistingBC", "OrgPR" });
    }

    [Fact]
    public async Task ImportTemplatesFromActivity_CopiesIntoTarget_SkippingExistingTypes()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var source = SeedActivity(db, orgA, "Source");
        var target = SeedActivity(db, orgA, "Target");
        SeedTemplates(db,
            Template(orgA, EmailTemplateType.BookingConfirmation, "SrcBC", isDefault: true, activityId: source),
            Template(orgA, EmailTemplateType.PaymentReminder, "SrcPR", isDefault: true, activityId: source),
            Template(orgA, EmailTemplateType.PaymentReminder, "TgtPR", isDefault: true, activityId: target));
        var sut = new EmailTemplateService(db.Context);

        var created = await sut.ImportTemplatesFromActivityAsync(orgA, source, target);

        created.Should().Be(1);
        (await sut.GetAllTemplatesAsync(orgA, target)).Select(t => t.Name)
            .Should().BeEquivalentTo(new[] { "TgtPR", "SrcBC" });
    }

    [Fact]
    public async Task GetActivitiesWithTemplates_ListsActivitiesAndCounts_ExcludingTarget()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        var a1 = SeedActivity(db, orgA, "Alpha");
        var a2 = SeedActivity(db, orgA, "Beta");
        SeedTemplates(db,
            Template(orgA, EmailTemplateType.Custom, "A1a", activityId: a1),
            Template(orgA, EmailTemplateType.PaymentReminder, "A1b", activityId: a1),
            Template(orgA, EmailTemplateType.Custom, "A2a", activityId: a2));
        var sut = new EmailTemplateService(db.Context);

        var result = await sut.GetActivitiesWithTemplatesAsync(orgA, excludeActivityId: a2);

        result.Should().ContainSingle();
        result[0].ActivityId.Should().Be(a1);
        result[0].ActivityName.Should().Be("Alpha");
        result[0].TemplateCount.Should().Be(2);
    }
}
