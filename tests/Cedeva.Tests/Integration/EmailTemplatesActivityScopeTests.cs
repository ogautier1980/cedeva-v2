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
    public async Task CreateActivity_CopiesOrganisationLibraryIntoTheNewActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            ctx.EmailTemplates.AddRange(
                OrgTemplate(org, EmailTemplateType.BookingConfirmation, "OrgBC"),
                OrgTemplate(org, EmailTemplateType.PaymentReminder, "OrgPR"));
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
        copied.Select(t => t.Name).Should().BeEquivalentTo(new[] { "OrgBC", "OrgPR" },
            "the org library is copied into the new activity");
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
        (await db.EmailTemplates.IgnoreQueryFilters().AnyAsync(t => t.ActivityId == target.Id && t.Name == "SrcBC"))
            .Should().BeTrue("the source activity's template is imported into the target");
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
