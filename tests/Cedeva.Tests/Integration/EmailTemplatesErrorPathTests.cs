using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Drives the defensive catch branches of <c>EmailTemplatesController</c> (Edit / Delete /
/// SetDefault) by forcing the persistence to fail via <see cref="ThrowingSaveChangesInterceptor"/>.
/// Each action wraps its service call in catch(InvalidOperationException) / catch(DbUpdateException)
/// / catch(Exception); throwing one of each type exercises all three. The controller must handle the
/// failure gracefully — never a 500 — and must not persist the change.
/// </summary>
[Collection("WebApp")]
public class EmailTemplatesErrorPathTests
{
    public static IEnumerable<object[]> Exceptions() => new[]
    {
        new object[] { "invalid-op" },
        new object[] { "db-update" },
        new object[] { "generic" },
    };

    private static Exception Make(string kind) => kind switch
    {
        "invalid-op" => new InvalidOperationException("boom"),
        "db-update" => new DbUpdateException("boom"),
        _ => new Exception("boom"),
    };

    private static (CedevaWebApplicationFactory factory, int orgId, int templateId) SeedTemplate()
    {
        var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        EmailTemplate template = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            template = new EmailTemplate
            {
                Organisation = org,
                Name = "Modèle",
                TemplateType = EmailTemplateType.Custom,
                Subject = "Sujet",
                HtmlContent = "<p>x</p>",
            };
            ctx.AddRange(org, template);
            return 0;
        });
        return (factory, org.Id, template.Id);
    }

    [Theory]
    [MemberData(nameof(Exceptions))]
    public async Task Edit_WhenSaveFails_IsHandledGracefully_AndNotPersisted(string kind)
    {
        var (factory, orgId, templateId) = SeedTemplate();
        using (factory)
        {
            var client = factory.CreateClientFor("u1", orgId, "Coordinator");
            factory.ThrowOnSaveChanges = Make(kind);

            var response = await client.PostAsync("/EmailTemplates/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = templateId.ToString(),
                ["Name"] = "Modifié",
                ["TemplateType"] = ((int)EmailTemplateType.Custom).ToString(),
                ["Subject"] = "Nouveau sujet",
                ["HtmlContent"] = "<p>modifié</p>",
            }));

            // A persistence failure must be caught, not surface as a 500.
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Found);

            factory.ThrowOnSaveChanges = null;
            using var db = factory.NewDbContext();
            var saved = await db.Set<EmailTemplate>().IgnoreQueryFilters().FirstAsync(t => t.Id == templateId);
            saved.Name.Should().Be("Modèle", "the failed edit must not persist");
        }
    }

    [Theory]
    [MemberData(nameof(Exceptions))]
    public async Task Delete_WhenSaveFails_IsHandledGracefully_AndNotDeleted(string kind)
    {
        var (factory, orgId, templateId) = SeedTemplate();
        using (factory)
        {
            var client = factory.CreateClientFor("u1", orgId, "Coordinator");
            factory.ThrowOnSaveChanges = Make(kind);

            var response = await client.PostAsync("/EmailTemplates/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = templateId.ToString(),
            }));

            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Found);

            factory.ThrowOnSaveChanges = null;
            using var db = factory.NewDbContext();
            (await db.Set<EmailTemplate>().IgnoreQueryFilters().AnyAsync(t => t.Id == templateId))
                .Should().BeTrue("the failed delete must leave the template in place");
        }
    }

    [Theory]
    [MemberData(nameof(Exceptions))]
    public async Task SetDefault_WhenSaveFails_IsHandledGracefully(string kind)
    {
        var (factory, orgId, templateId) = SeedTemplate();
        using (factory)
        {
            var client = factory.CreateClientFor("u1", orgId, "Coordinator");
            factory.ThrowOnSaveChanges = Make(kind);

            var response = await client.PostAsync("/EmailTemplates/SetDefault", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = templateId.ToString(),
                ["type"] = ((int)EmailTemplateType.Custom).ToString(),
            }));

            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Found);

            factory.ThrowOnSaveChanges = null;
            using var db = factory.NewDbContext();
            (await db.Set<EmailTemplate>().IgnoreQueryFilters().FirstAsync(t => t.Id == templateId))
                .IsDefault.Should().BeFalse("the failed set-default must not persist");
        }
    }
}
