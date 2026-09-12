using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage for org-level default question templates (Lot J — "modèle de questions par
/// activité"): CRUD on the templates page, and that a newly created activity is seeded with a copy
/// of the organisation's templates (via both <c>ActivitiesController.Create</c> and the
/// <c>ActivityWizardController</c> Step1).
/// </summary>
[Collection("WebApp")]
public class QuestionTemplatesControllerTests
{
    [Fact]
    public async Task Create_PersistsTemplate()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx => { org = TestData.Organisation(); ctx.Add(org); return 0; });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync("/QuestionTemplates/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["QuestionText"] = "Allergies alimentaires ?",
                ["QuestionType"] = ((int)QuestionType.Text).ToString(),
                ["IsRequired"] = "true",
                ["DisplayOrder"] = "1",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var template = await db.OrganisationQuestionTemplates.IgnoreQueryFilters().SingleAsync(t => t.OrganisationId == org.Id);
        template.QuestionText.Should().Be("Allergies alimentaires ?");
        template.IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Edit_UpdatesTemplate()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        OrganisationQuestionTemplate template = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            template = new OrganisationQuestionTemplate { Organisation = org, QuestionText = "Ancien texte", QuestionType = QuestionType.Text, DisplayOrder = 1 };
            ctx.Add(org);
            ctx.Add(template);
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync("/QuestionTemplates/Edit", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Id"] = template.Id.ToString(),
                ["QuestionText"] = "Nouveau texte",
                ["QuestionType"] = ((int)QuestionType.Text).ToString(),
                ["DisplayOrder"] = "1",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        (await db.OrganisationQuestionTemplates.IgnoreQueryFilters().SingleAsync(t => t.Id == template.Id)).QuestionText.Should().Be("Nouveau texte");
    }

    [Fact]
    public async Task Delete_RemovesTemplate()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        OrganisationQuestionTemplate template = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            template = new OrganisationQuestionTemplate { Organisation = org, QuestionText = "À supprimer", QuestionType = QuestionType.Text, DisplayOrder = 1 };
            ctx.Add(org);
            ctx.Add(template);
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync("/QuestionTemplates/Delete", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = template.Id.ToString() }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        (await db.OrganisationQuestionTemplates.IgnoreQueryFilters().AnyAsync(t => t.Id == template.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ActivitiesCreate_SeedsNewActivityWithOrganisationTemplates()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            ctx.Add(new OrganisationQuestionTemplate { Organisation = org, QuestionText = "Numéro de sécurité sociale ?", QuestionType = QuestionType.Text, DisplayOrder = 1 });
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync("/Activities/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "Stage avec modèle",
                ["Description"] = "Description",
                ["StartDate"] = "2026-07-01",
                ["EndDate"] = "2026-07-05",
                ["IsActive"] = "true",
                ["OrganisationId"] = org.Id.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var activity = await db.Activities.IgnoreQueryFilters().SingleAsync(a => a.Name == "Stage avec modèle");
        (await db.ActivityQuestions.AnyAsync(q => q.ActivityId == activity.Id && q.QuestionText == "Numéro de sécurité sociale ?"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ActivityWizardStep1_SeedsNewActivityWithOrganisationTemplates()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            ctx.Add(new OrganisationQuestionTemplate { Organisation = org, QuestionText = "Personne à contacter en cas d'urgence ?", QuestionType = QuestionType.Text, DisplayOrder = 1 });
            return 0;
        });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step1", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "Stage Wizard Modèle",
                ["StartDate"] = "2026-07-01",
                ["EndDate"] = "2026-07-05",
                ["OrganisationId"] = org.Id.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var activity = await db.Activities.IgnoreQueryFilters().SingleAsync(a => a.Name == "Stage Wizard Modèle");
        (await db.ActivityQuestions.AnyAsync(q => q.ActivityId == activity.Id && q.QuestionText == "Personne à contacter en cas d'urgence ?"))
            .Should().BeTrue();
    }
}
