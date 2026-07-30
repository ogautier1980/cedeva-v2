using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// End-to-end coverage of the activity-scoped email templates (Lot 4): the organisation library is
/// copied into a newly created activity, templates can be created at activity scope, and one
/// activity's templates can be imported into another.
/// </summary>
[Collection("WebApp")]
public class EmailTemplatesActivityScopeTests
{
    private static EmailTemplate OrgTemplate(Organisation org, EmailTemplateType type, string name) => new()
    {
        Organisation = org,
        ActivityId = null,
        TemplateType = type,
        Name = name,
        Subject = "S " + name,
        HtmlContent = "<p>" + name + "</p>",
        IsDefault = true
    };

    private static void SeedUser(CedevaDbContext ctx, string id, int organisationId)
    {
        ctx.Add(new CedevaUser
        {
            Id = id,
            UserName = $"{id}@test.be",
            NormalizedUserName = $"{id}@TEST.BE",
            Email = $"{id}@test.be",
            NormalizedEmail = $"{id}@TEST.BE",
            OrganisationId = organisationId,
            Role = Role.Coordinator
        });
    }

    [Fact]
    public async Task CreateActivity_CopiesOrganisationLibraryIntoTheNewActivity_ExceptLockedTypes()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            ctx.EmailTemplates.AddRange(
                // Locked types (decision 2026-07-30, Lot E) must never be copied into an activity.
                OrgTemplate(org, EmailTemplateType.BookingConfirmation, "OrgBC"),
                OrgTemplate(org, EmailTemplateType.PaymentReminder, "OrgPR"),
                OrgTemplate(org, EmailTemplateType.MedicalSheetReminder, "OrgMSR"),
                // Non-locked types still get copied as before.
                OrgTemplate(org, EmailTemplateType.Custom, "OrgCustom"));
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Stage avec modèles",
            ["Description"] = "desc",
            ["StartDate"] = "2026-07-01",
            ["EndDate"] = "2026-07-05",
            ["IsActive"] = "true",
            ["PricePerDay"] = "20",
            ["OrganisationId"] = "0",
            ["Id"] = "0"
        });
        var response = await client.PostAsync("/Activities/Create", form);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var activity = await db.Activities.IgnoreQueryFilters().FirstAsync(a => a.Name == "Stage avec modèles");
        var copied = await db.EmailTemplates.IgnoreQueryFilters()
            .Where(t => t.ActivityId == activity.Id).ToListAsync();
        copied.Select(t => t.Name).Should().BeEquivalentTo(new[] { "OrgCustom" },
            "only non-locked types are copied into the new activity");
    }

    [Fact]
    public async Task Create_WithActivityId_PersistsActivityScopedTemplate()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            ctx.SaveChanges();
            SeedUser(ctx, "u1", org.Id);
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Modèle activité",
            ["TemplateType"] = ((int)EmailTemplateType.Custom).ToString(),
            ["Subject"] = "Sujet",
            ["HtmlContent"] = "<p>Corps</p>",
            ["ActivityId"] = activity.Id.ToString()
        });
        var response = await client.PostAsync("/EmailTemplates/Create", form);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var template = await db.EmailTemplates.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Name == "Modèle activité");
        template.Should().NotBeNull();
        template!.ActivityId.Should().Be(activity.Id);
        template.IsDefault.Should().BeTrue("first of its type in the scope becomes the default");
    }

    [Fact]
    public async Task Import_CopiesTemplatesFromSourceActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity source = null!, target = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            source = TestData.Activity(org, "Source");
            target = TestData.Activity(org, "Target");
            ctx.AddRange(org, source, target);
            return 0;
        });
        factory.Seed(ctx =>
        {
            ctx.EmailTemplates.AddRange(
                new EmailTemplate { OrganisationId = org.Id, ActivityId = source.Id, TemplateType = EmailTemplateType.Custom, Name = "SrcCustom", Subject = "s", HtmlContent = "<p>x</p>", IsDefault = true },
                // Locked type: must not be importable into another activity (decision 2026-07-30, Lot E).
                new EmailTemplate { OrganisationId = org.Id, ActivityId = source.Id, TemplateType = EmailTemplateType.BookingConfirmation, Name = "SrcBC", Subject = "s", HtmlContent = "<p>x</p>", IsDefault = true });
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["activityId"] = target.Id.ToString(),
            ["sourceActivityId"] = source.Id.ToString()
        });
        var response = await client.PostAsync("/EmailTemplates/Import", form);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        (await db.EmailTemplates.IgnoreQueryFilters().AnyAsync(t => t.ActivityId == target.Id && t.Name == "SrcCustom"))
            .Should().BeTrue("the source activity's non-locked template is imported into the target");
        (await db.EmailTemplates.IgnoreQueryFilters().AnyAsync(t => t.ActivityId == target.Id && t.Name == "SrcBC"))
            .Should().BeFalse("locked types are never imported into an activity");
    }

    [Fact]
    public async Task Create_LockedTypeWithActivityId_IsRejected()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            ctx.SaveChanges();
            SeedUser(ctx, "u1", org.Id);
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Nouvelle confirmation",
            ["TemplateType"] = ((int)EmailTemplateType.BookingConfirmation).ToString(),
            ["Subject"] = "Sujet",
            ["HtmlContent"] = "<p>Corps</p>",
            ["ActivityId"] = activity.Id.ToString()
        });
        var response = await client.PostAsync("/EmailTemplates/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the locked type is rejected and the Create form is redisplayed");
        using var db = factory.NewDbContext();
        (await db.EmailTemplates.IgnoreQueryFilters().AnyAsync(t => t.Name == "Nouvelle confirmation"))
            .Should().BeFalse("a locked type cannot be created, even scoped to an activity");
    }

    [Fact]
    public async Task Delete_LockedType_IsRejected()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        EmailTemplate template = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            SeedUser(ctx, "u1", org.Id);
            template = OrgTemplate(org, EmailTemplateType.PaymentReminder, "OrgPR");
            ctx.EmailTemplates.Add(template);
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync("/EmailTemplates/Delete",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = template.Id.ToString() }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var db = factory.NewDbContext();
        (await db.EmailTemplates.IgnoreQueryFilters().AnyAsync(t => t.Id == template.Id))
            .Should().BeTrue("a locked type cannot be deleted");
    }

    [Fact]
    public async Task Duplicate_LockedType_IsRejected()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        EmailTemplate template = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            SeedUser(ctx, "u1", org.Id);
            template = OrgTemplate(org, EmailTemplateType.MedicalSheetReminder, "OrgMSR");
            ctx.EmailTemplates.Add(template);
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.GetAsync($"/EmailTemplates/Duplicate/{template.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Found, "duplication is rejected with a redirect back to Index, not the Create form");
        using var db = factory.NewDbContext();
        (await db.EmailTemplates.IgnoreQueryFilters().CountAsync(t => t.TemplateType == EmailTemplateType.MedicalSheetReminder))
            .Should().Be(1, "no copy was created");
    }

    [Fact]
    public async Task SaveFromEmail_CreatesActivityScopedTemplateFromComposedEmail()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            ctx.SaveChanges();
            SeedUser(ctx, "u1", org.Id);
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["activityId"] = activity.Id.ToString(),
            ["name"] = "Mon modèle perso",
            ["templateType"] = ((int)EmailTemplateType.Custom).ToString(),
            ["subject"] = "Sujet composé",
            ["message"] = "<p>Contenu composé</p>"
        });
        var response = await client.PostAsync("/EmailTemplates/SaveFromEmail", form);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var template = await db.EmailTemplates.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Name == "Mon modèle perso");
        template.Should().NotBeNull();
        template!.ActivityId.Should().Be(activity.Id);
        template.Subject.Should().Be("Sujet composé");
        template.HtmlContent.Should().Be("<p>Contenu composé</p>");
    }

    [Fact]
    public async Task DeleteActivity_RemovesItsTemplates_ButKeepsOrgLibrary()
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
        factory.Seed(ctx =>
        {
            ctx.EmailTemplates.AddRange(
                new EmailTemplate { OrganisationId = org.Id, ActivityId = null, TemplateType = EmailTemplateType.Custom, Name = "OrgKeep", Subject = "s", HtmlContent = "<p>x</p>", IsDefault = true },
                new EmailTemplate { OrganisationId = org.Id, ActivityId = activity.Id, TemplateType = EmailTemplateType.Custom, Name = "ActivityGone", Subject = "s", HtmlContent = "<p>x</p>", IsDefault = true });
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync($"/Activities/Delete/{activity.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = activity.Id.ToString() }));
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        (await db.EmailTemplates.IgnoreQueryFilters().AnyAsync(t => t.Name == "ActivityGone"))
            .Should().BeFalse("the activity's templates are removed with it");
        (await db.EmailTemplates.IgnoreQueryFilters().AnyAsync(t => t.Name == "OrgKeep"))
            .Should().BeTrue("the organisation library is untouched");
    }

    [Fact]
    public async Task Index_OrgScope_ShowsOnlyOrgLibrary_NotActivityTemplates()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            ctx.SaveChanges();
            ctx.EmailTemplates.AddRange(
                new EmailTemplate { OrganisationId = org.Id, ActivityId = null, TemplateType = EmailTemplateType.Custom, Name = "OrgLevelOnly", Subject = "s", HtmlContent = "<p>x</p>", IsDefault = true },
                new EmailTemplate { OrganisationId = org.Id, ActivityId = activity.Id, TemplateType = EmailTemplateType.Custom, Name = "ActivityLevelOnly", Subject = "s", HtmlContent = "<p>x</p>", IsDefault = true });
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var orgBody = await (await client.GetAsync("/EmailTemplates")).Content.ReadAsStringAsync();
        orgBody.Should().Contain("OrgLevelOnly");
        orgBody.Should().NotContain("ActivityLevelOnly");

        var activityBody = await (await client.GetAsync($"/EmailTemplates?activityId={activity.Id}")).Content.ReadAsStringAsync();
        activityBody.Should().Contain("ActivityLevelOnly");
        activityBody.Should().NotContain("OrgLevelOnly");
    }
}
