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
        bool isDefault = false) => new()
    {
        OrganisationId = organisationId,
        TemplateType = type,
        Name = name,
        Subject = "Subject " + name,
        HtmlContent = "<p>" + name + "</p>",
        IsDefault = isDefault
    };

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
}
