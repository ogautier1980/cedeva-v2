using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Additional coverage for <c>EmailTemplatesController</c>, complementing
/// <c>EmailTemplatesControllerIntegrationTests</c>. Focuses on branches not already
/// exercised: Index type-filter + activity-session branches, GET Create form contents,
/// Duplicate GET (found/unknown), Edit POST unknown template, Delete edge cases
/// (unknown id / tenant isolation), SetDefault unknown id, and unauthenticated access
/// to the POST and AJAX endpoints.
/// </summary>
[Collection("WebApp")]
public class EmailTemplatesControllerMoreTests
{
    // ---- helpers -------------------------------------------------------------

    private static EmailTemplate Template(Organisation org, string name = "Modèle Test",
        EmailTemplateType type = EmailTemplateType.Custom, bool isDefault = false, bool isShared = false) => new()
    {
        Organisation = org,
        Name = name,
        TemplateType = type,
        Subject = "Sujet de test",
        HtmlContent = "<p>Bonjour</p>",
        IsDefault = isDefault,
        IsShared = isShared
    };

    private static CedevaUser SeedUser(CedevaDbContext ctx, string id, int? organisationId)
    {
        var user = new CedevaUser
        {
            Id = id,
            UserName = $"{id}@test.be",
            NormalizedUserName = $"{id}@TEST.BE".ToUpperInvariant(),
            Email = $"{id}@test.be",
            NormalizedEmail = $"{id}@TEST.BE".ToUpperInvariant(),
            OrganisationId = organisationId,
            Role = Role.Coordinator
        };
        ctx.Add(user);
        return user;
    }

    private static WebApplicationFactoryClientOptions NoRedirect() =>
        new() { AllowAutoRedirect = false };

    // ---- GET Index: type filter branch --------------------------------------

    [Fact]
    public async Task Index_WithTypeFilter_ShowsOnlyMatchingTypeTemplates()
    {
        using var factory = new CedevaWebApplicationFactory();
        int orgId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            orgId = org.Id;
            ctx.AddRange(
                Template(org, "ReminderTemplate", EmailTemplateType.PaymentReminder),
                Template(org, "CustomTemplate", EmailTemplateType.Custom));
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var response = await client.GetAsync(
            $"/EmailTemplates?type={(int)EmailTemplateType.PaymentReminder}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("ReminderTemplate");
        html.Should().NotContain("CustomTemplate");
    }

    [Fact]
    public async Task Index_NoTemplates_RendersEmptyState()
    {
        using var factory = new CedevaWebApplicationFactory();
        int orgId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            orgId = org.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var response = await client.GetAsync("/EmailTemplates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // No templates seeded for this org -> the table block must not appear.
        html.Should().NotContain("table-hover");
    }

    // ---- GET Index: activity-session branch ---------------------------------

    [Fact]
    public async Task Index_WithActivityId_StoresActivityInSessionAndScopesTemplates()
    {
        using var factory = new CedevaWebApplicationFactory();
        int activityId = 0;
        int orgId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            orgId = org.Id;

            // The activity lookup is itself tenant-filtered, so the activity must belong to
            // the caller's organisation for the activity branch (SetActivityViewData) to run.
            var activity = TestData.Activity(org, "Stage Activite");
            ctx.Add(activity);
            ctx.SaveChanges();
            activityId = activity.Id;

            ctx.Add(Template(org, "ActivityScopedTemplate"));
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var response = await client.GetAsync($"/EmailTemplates?id={activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("ActivityScopedTemplate");
    }

    [Fact]
    public async Task Index_WithUnknownActivityId_DoesNotThrow()
    {
        using var factory = new CedevaWebApplicationFactory();
        int orgId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            orgId = org.Id;
            ctx.Add(Template(org, "OwnTemplate"));
            return 0;
        });

        // Unknown activity id: the activity lookup returns null, so org filtering falls back
        // to the caller's own organisation.
        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var response = await client.GetAsync("/EmailTemplates?id=987654");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("OwnTemplate");
    }

    // ---- GET Create: form contents ------------------------------------------

    [Fact]
    public async Task Create_Get_RendersFormWithTypeOptions()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/EmailTemplates/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("name=\"Name\"");
        html.Should().Contain("name=\"Subject\"");
    }

    // ---- GET Duplicate -------------------------------------------------------

    [Fact]
    public async Task Duplicate_Get_ExistingTemplate_RendersCreateViewAsCopy()
    {
        using var factory = new CedevaWebApplicationFactory();
        int templateId = 0;
        int orgId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            orgId = org.Id;
            var t = Template(org, "ModeleSource", isDefault: true);
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var response = await client.GetAsync($"/EmailTemplates/Duplicate/{templateId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // Controller pre-fills the name with " (Copy)" and renders the Create view.
        html.Should().Contain("ModeleSource (Copy)");
        html.Should().Contain("name=\"Name\"");

        // Duplicate is a pure GET; nothing is persisted.
        using var db = factory.NewDbContext();
        var count = await db.EmailTemplates.IgnoreQueryFilters().CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_Get_UnknownTemplate_RedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/EmailTemplates/Duplicate/555555");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("EmailTemplates");
    }

    [Fact]
    public async Task Duplicate_Get_TemplateOfAnotherOrganisation_RedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        int templateId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            var t = Template(org, "ForeignSource");
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        // Different org -> query filter hides the template -> treated as not found.
        var client = factory.CreateClientFor("u1", organisationId: 99999, role: "Coordinator");
        var response = await client.GetAsync($"/EmailTemplates/Duplicate/{templateId}");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("EmailTemplates");
    }

    // ---- POST Edit: unknown template ----------------------------------------

    [Fact]
    public async Task Edit_Post_UnknownTemplate_RedirectsAndPersistsNothing()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "777777",
            ["Name"] = "Inexistant",
            ["TemplateType"] = ((int)EmailTemplateType.Custom).ToString(),
            ["Subject"] = "Sujet",
            ["HtmlContent"] = "<p>x</p>"
        });

        var response = await client.PostAsync("/EmailTemplates/Edit", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("EmailTemplates");

        using var db = factory.NewDbContext();
        var count = await db.EmailTemplates.IgnoreQueryFilters().CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Edit_Post_SetIsDefault_UnsetsPreviousDefaultOfSameType()
    {
        using var factory = new CedevaWebApplicationFactory();
        int targetId = 0;
        int previousDefaultId = 0;
        int orgId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            orgId = org.Id;
            var oldDefault = Template(org, "AncienDefaut", EmailTemplateType.PaymentReminder, isDefault: true);
            var target = Template(org, "NouveauDefaut", EmailTemplateType.PaymentReminder, isDefault: false);
            ctx.AddRange(oldDefault, target);
            ctx.SaveChanges();
            previousDefaultId = oldDefault.Id;
            targetId = target.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = targetId.ToString(),
            ["Name"] = "NouveauDefaut",
            ["TemplateType"] = ((int)EmailTemplateType.PaymentReminder).ToString(),
            ["Subject"] = "Sujet",
            ["HtmlContent"] = "<p>x</p>",
            ["IsDefault"] = "true"
        });

        var response = await client.PostAsync("/EmailTemplates/Edit", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var target = await db.EmailTemplates.IgnoreQueryFilters().FirstAsync(t => t.Id == targetId);
        var old = await db.EmailTemplates.IgnoreQueryFilters().FirstAsync(t => t.Id == previousDefaultId);
        target.IsDefault.Should().BeTrue();
        old.IsDefault.Should().BeFalse();
    }

    // ---- POST Delete: edge cases --------------------------------------------

    [Fact]
    public async Task Delete_Post_UnknownId_RedirectsWithoutError()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "666666"
        });

        var response = await client.PostAsync("/EmailTemplates/Delete", form);

        // Service no-ops when the template is not found; controller still redirects.
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("EmailTemplates");
    }

    [Fact]
    public async Task Delete_Post_TemplateOfAnotherOrganisation_DoesNotDelete()
    {
        using var factory = new CedevaWebApplicationFactory();
        int templateId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            var t = Template(org, "Protege");
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        // Caller of a different org: FindAsync honours the tenant filter -> not found -> no delete.
        var client = factory.CreateClientFor("u1", organisationId: 99999, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = templateId.ToString()
        });

        var response = await client.PostAsync("/EmailTemplates/Delete", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var exists = await db.EmailTemplates.IgnoreQueryFilters().AnyAsync(t => t.Id == templateId);
        exists.Should().BeTrue();
    }

    // ---- POST SetDefault: unknown id ----------------------------------------

    [Fact]
    public async Task SetDefault_Post_UnknownId_RedirectsWithoutThrowing()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "333333",
            ["type"] = ((int)EmailTemplateType.Custom).ToString()
        });

        var response = await client.PostAsync("/EmailTemplates/SetDefault", form);

        // Service throws InvalidOperationException for a missing template; controller catches
        // it and redirects to Index.
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("EmailTemplates");
    }

    // ---- Unauthenticated access ---------------------------------------------

    [Fact]
    public async Task Create_Post_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(NoRedirect());
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "X",
            ["TemplateType"] = ((int)EmailTemplateType.Custom).ToString(),
            ["Subject"] = "Y",
            ["HtmlContent"] = "<p>z</p>"
        });

        var response = await client.PostAsync("/EmailTemplates/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Post_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(NoRedirect());
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "1" });

        var response = await client.PostAsync("/EmailTemplates/Delete", form);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetDefault_Post_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(NoRedirect());
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "1",
            ["type"] = ((int)EmailTemplateType.Custom).ToString()
        });

        var response = await client.PostAsync("/EmailTemplates/SetDefault", form);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTemplate_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(NoRedirect());
        var response = await client.GetAsync("/EmailTemplates/GetTemplate?id=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Duplicate_Get_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(NoRedirect());
        var response = await client.GetAsync("/EmailTemplates/Duplicate/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
