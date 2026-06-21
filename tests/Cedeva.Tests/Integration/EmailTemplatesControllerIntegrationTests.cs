using System.Net;
using System.Net.Http;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class EmailTemplatesControllerIntegrationTests
{
    // ---- helpers -------------------------------------------------------------

    private static EmailTemplate Template(Organisation org, string name = "Modèle Test",
        EmailTemplateType type = EmailTemplateType.Custom, bool isDefault = false, int? activityId = null) => new()
    {
        Organisation = org,
        Name = name,
        TemplateType = type,
        Subject = "Sujet de test",
        HtmlContent = "<p>Bonjour</p>",
        IsDefault = isDefault,
        ActivityId = activityId
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

    private static FormUrlEncodedContent ValidCreateForm() => new(new Dictionary<string, string>
    {
        ["Name"] = "Nouveau modèle",
        ["TemplateType"] = ((int)EmailTemplateType.Custom).ToString(),
        ["Subject"] = "Sujet du mail",
        ["HtmlContent"] = "<p>Contenu</p>"
    });

    // ---- GET Index -----------------------------------------------------------

    [Fact]
    public async Task Index_AuthenticatedCoordinator_RendersOwnOrganisationTemplates()
    {
        using var factory = new CedevaWebApplicationFactory();
        int orgId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            orgId = org.Id;
            ctx.Add(Template(org, "ModeleVisible"));
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var response = await client.GetAsync("/EmailTemplates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("ModeleVisible");
    }

    [Fact]
    public async Task Index_CoordinatorOfAnotherOrganisation_DoesNotSeeForeignTemplates()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            ctx.Add(Template(org, "ModeleEtranger"));
            return 0;
        });

        // Coordinator belongs to a different organisation than the seeded template.
        var client = factory.CreateClientFor("u1", organisationId: 99999, role: "Coordinator");
        var response = await client.GetAsync("/EmailTemplates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("ModeleEtranger");
    }

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/EmailTemplates");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- GET Create ----------------------------------------------------------

    [Fact]
    public async Task Create_Get_ReturnsFormView()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/EmailTemplates/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- POST Create ---------------------------------------------------------

    [Fact]
    public async Task Create_Post_ValidWithKnownUser_PersistsAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        int orgId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            orgId = org.Id;
            // The controller resolves the current user via UserManager.GetUserAsync,
            // so the principal's NameIdentifier ("u1") must map to a real CedevaUser.
            SeedUser(ctx, "u1", orgId);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var response = await client.PostAsync("/EmailTemplates/Create", ValidCreateForm());

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("EmailTemplates");

        using var db = factory.NewDbContext();
        var saved = await db.EmailTemplates.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Name == "Nouveau modèle");
        saved.Should().NotBeNull();
        saved!.OrganisationId.Should().Be(orgId);
        saved.Subject.Should().Be("Sujet du mail");
        saved.CreatedBy.Should().Be("u1");
    }

    [Fact]
    public async Task Create_Post_UnknownUser_RedirectsAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        // No CedevaUser with Id "u1" exists -> GetUserAsync returns null -> redirect, no insert.
        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.PostAsync("/EmailTemplates/Create", ValidCreateForm());

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var count = await db.EmailTemplates.IgnoreQueryFilters().CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Create_Post_MissingRequiredFields_ReturnsViewWithoutPersisting()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            SeedUser(ctx, "u1", org.Id);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        // Missing Name, Subject and HtmlContent (all [Required]).
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TemplateType"] = ((int)EmailTemplateType.Custom).ToString()
        });

        var response = await client.PostAsync("/EmailTemplates/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-rendered view, not a redirect

        using var db = factory.NewDbContext();
        var count = await db.EmailTemplates.IgnoreQueryFilters().CountAsync();
        count.Should().Be(0);
    }

    // ---- GET Edit ------------------------------------------------------------

    [Fact]
    public async Task Edit_Get_ExistingTemplate_ReturnsFormView()
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
            var t = Template(org, "ModeleEditable");
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var response = await client.GetAsync($"/EmailTemplates/Edit/{templateId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("ModeleEditable");
    }

    [Fact]
    public async Task Edit_Get_UnknownTemplate_RedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/EmailTemplates/Edit/424242");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("EmailTemplates");
    }

    [Fact]
    public async Task Edit_Get_TemplateOfAnotherOrganisation_RedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        int templateId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            var t = Template(org, "Foreign");
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        // Coordinator of a different organisation: query filter hides it -> treated as not found.
        var client = factory.CreateClientFor("u1", organisationId: 99999, role: "Coordinator");
        var response = await client.GetAsync($"/EmailTemplates/Edit/{templateId}");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("EmailTemplates");
    }

    // ---- POST Edit -----------------------------------------------------------

    [Fact]
    public async Task Edit_Post_Valid_UpdatesAndRedirects()
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
            var t = Template(org, "Ancien Nom");
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = templateId.ToString(),
            ["Name"] = "Nouveau Nom",
            ["TemplateType"] = ((int)EmailTemplateType.PaymentReminder).ToString(),
            ["Subject"] = "Sujet modifié",
            ["HtmlContent"] = "<p>Modifié</p>"
        });

        var response = await client.PostAsync("/EmailTemplates/Edit", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var updated = await db.EmailTemplates.IgnoreQueryFilters()
            .FirstAsync(t => t.Id == templateId);
        updated.Name.Should().Be("Nouveau Nom");
        updated.TemplateType.Should().Be(EmailTemplateType.PaymentReminder);
        updated.Subject.Should().Be("Sujet modifié");
    }

    [Fact]
    public async Task Edit_Post_MissingRequiredFields_ReturnsViewWithoutUpdating()
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
            var t = Template(org, "Nom Intact");
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = templateId.ToString(),
            ["TemplateType"] = ((int)EmailTemplateType.Custom).ToString()
            // Name, Subject, HtmlContent missing -> invalid
        });

        var response = await client.PostAsync("/EmailTemplates/Edit", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var unchanged = await db.EmailTemplates.IgnoreQueryFilters().FirstAsync(t => t.Id == templateId);
        unchanged.Name.Should().Be("Nom Intact");
    }

    // ---- POST Delete ---------------------------------------------------------

    [Fact]
    public async Task Delete_Post_ExistingTemplate_RemovesAndRedirects()
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
            var t = Template(org, "À Supprimer");
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = templateId.ToString()
        });

        var response = await client.PostAsync("/EmailTemplates/Delete", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var exists = await db.EmailTemplates.IgnoreQueryFilters().AnyAsync(t => t.Id == templateId);
        exists.Should().BeFalse();
    }

    // ---- POST SetDefault -----------------------------------------------------

    [Fact]
    public async Task SetDefault_Post_MarksTemplateDefaultAndUnsetsOthers()
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
            var oldDefault = Template(org, "Ancien défaut", EmailTemplateType.PaymentReminder, isDefault: true);
            var target = Template(org, "Cible", EmailTemplateType.PaymentReminder, isDefault: false);
            ctx.AddRange(oldDefault, target);
            ctx.SaveChanges();
            previousDefaultId = oldDefault.Id;
            targetId = target.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = targetId.ToString(),
            ["type"] = ((int)EmailTemplateType.PaymentReminder).ToString()
        });

        var response = await client.PostAsync("/EmailTemplates/SetDefault", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var target = await db.EmailTemplates.IgnoreQueryFilters().FirstAsync(t => t.Id == targetId);
        var old = await db.EmailTemplates.IgnoreQueryFilters().FirstAsync(t => t.Id == previousDefaultId);
        target.IsDefault.Should().BeTrue();
        old.IsDefault.Should().BeFalse();
    }

    // ---- GET GetTemplate (AJAX) ----------------------------------------------

    [Fact]
    public async Task GetTemplate_ExistingTemplate_ReturnsJson()
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
            var t = Template(org, "JsonModele");
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: orgId, role: "Coordinator");
        var response = await client.GetAsync($"/EmailTemplates/GetTemplate?id={templateId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("JsonModele");
    }

    [Fact]
    public async Task GetTemplate_UnknownTemplate_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/EmailTemplates/GetTemplate?id=999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTemplate_TemplateOfAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        int templateId = 0;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            var t = Template(org, "Secret");
            ctx.Add(t);
            ctx.SaveChanges();
            templateId = t.Id;
            return 0;
        });

        // Coordinator of another org: query filter hides the template -> NotFound.
        var client = factory.CreateClientFor("u1", organisationId: 99999, role: "Coordinator");
        var response = await client.GetAsync($"/EmailTemplates/GetTemplate?id={templateId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
