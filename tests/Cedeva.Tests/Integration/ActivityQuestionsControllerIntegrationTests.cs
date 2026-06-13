using System.Net;
using System.Net.Http.Json;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class ActivityQuestionsControllerIntegrationTests
{
    private static Dictionary<string, string> CreateForm(
        int activityId,
        string questionText = "Allergies?",
        QuestionType type = QuestionType.Text,
        string? options = null,
        bool isRequired = false)
    {
        var form = new Dictionary<string, string>
        {
            ["ActivityId"] = activityId.ToString(),
            ["QuestionText"] = questionText,
            ["QuestionType"] = ((int)type).ToString(),
            ["IsRequired"] = isRequired.ToString(),
        };
        if (options != null)
            form["Options"] = options;
        return form;
    }

    // ---------------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/ActivityQuestions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePost_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/ActivityQuestions/Create",
            new FormUrlEncodedContent(CreateForm(1)));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // GET Index
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithQueryParams_RedirectsToCleanUrl()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityQuestions?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Found); // 302 to clean URL
        response.Headers.Location!.ToString().Should().Contain("ActivityQuestions");
    }

    [Fact]
    public async Task Index_NoQueryParams_ListsQuestions()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org, "Stage Questions");
            var q = TestData.Question(activity, "Quelle est ta couleur preferee?");
            ctx.AddRange(org, activity, q);
            return 0;
        });

        // The questions list Includes the (tenancy-filtered) Activity, so the coordinator must
        // belong to the activity's organisation to see them.
        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");
        var response = await client.GetAsync("/ActivityQuestions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Quelle est ta couleur preferee?");
    }

    // ---------------------------------------------------------------------
    // GET Create
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreateForm_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityQuestions/Create?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // POST Create
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreatePost_Valid_RedirectsAndPersists()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync("/ActivityQuestions/Create",
            new FormUrlEncodedContent(CreateForm(activity.Id, "Des allergies?", QuestionType.Text)));

        response.StatusCode.Should().Be(HttpStatusCode.Found); // 302
        response.Headers.Location!.ToString().Should().Contain("ActivityQuestions"); // RedirectToAction(Index) => /ActivityQuestions

        using var db = factory.NewDbContext();
        var saved = db.ActivityQuestions.Where(q => q.QuestionText == "Des allergies?").ToList();
        saved.Should().HaveCount(1);
        saved[0].ActivityId.Should().Be(activity.Id);
        saved[0].QuestionType.Should().Be(QuestionType.Text);
    }

    [Fact]
    public async Task CreatePost_MissingQuestionText_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var form = CreateForm(activity.Id);
        form["QuestionText"] = ""; // required

        var response = await client.PostAsync("/ActivityQuestions/Create",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-render with ModelState errors

        using var db = factory.NewDbContext();
        db.ActivityQuestions.Count().Should().Be(0);
    }

    [Fact]
    public async Task CreatePost_RadioWithoutOptions_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        // Radio type but no Options -> server-side OptionsRequired validation.
        var response = await client.PostAsync("/ActivityQuestions/Create",
            new FormUrlEncodedContent(CreateForm(activity.Id, "Choisis", QuestionType.Radio, options: null)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.ActivityQuestions.Count().Should().Be(0);
    }

    [Fact]
    public async Task CreatePost_DropdownWithOptions_RedirectsAndPersists()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync("/ActivityQuestions/Create",
            new FormUrlEncodedContent(CreateForm(activity.Id, "Choisis ta taille", QuestionType.Dropdown, options: "S,M,L")));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var saved = db.ActivityQuestions.Single(q => q.QuestionText == "Choisis ta taille");
        saved.Options.Should().Be("S,M,L");
        saved.QuestionType.Should().Be(QuestionType.Dropdown);
    }

    // ---------------------------------------------------------------------
    // GET Edit
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EditForm_ExistingQuestion_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion question = null!;
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org);
            question = TestData.Question(activity, "Question existante");
            ctx.AddRange(org, activity, question);
            return 0;
        });

        // Edit Includes the tenancy-filtered Activity, so the coordinator must own its organisation.
        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityQuestions/Edit/{question.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Question existante");
    }

    [Fact]
    public async Task EditForm_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync("/ActivityQuestions/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // POST Edit
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EditPost_Valid_RedirectsAndUpdates()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion question = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            question = TestData.Question(activity, "Ancien texte");
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var form = CreateForm(activity.Id, "Nouveau texte", QuestionType.Text);
        form["Id"] = question.Id.ToString();

        var response = await client.PostAsync($"/ActivityQuestions/Edit/{question.Id}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var updated = db.ActivityQuestions.Single(q => q.Id == question.Id);
        updated.QuestionText.Should().Be("Nouveau texte");
    }

    [Fact]
    public async Task EditPost_UnknownId_ReturnsNotFound()
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

        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");
        // Valid model, id == model.Id, but no such question exists -> FindAsync null -> NotFound.
        // (A route/model id "mismatch" is not reachable over HTTP: the complex-type Id property
        // binds from the route value, so it always equals the route id.)
        var form = CreateForm(activity.Id, "Texte", QuestionType.Text);
        form["Id"] = "999999";

        var response = await client.PostAsync("/ActivityQuestions/Edit/999999",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditPost_MissingQuestionText_ReturnsViewAndDoesNotUpdate()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion question = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            question = TestData.Question(activity, "Texte original");
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var form = CreateForm(activity.Id, "", QuestionType.Text);
        form["Id"] = question.Id.ToString();

        var response = await client.PostAsync($"/ActivityQuestions/Edit/{question.Id}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.ActivityQuestions.Single(q => q.Id == question.Id).QuestionText.Should().Be("Texte original");
    }

    // ---------------------------------------------------------------------
    // GET Delete + POST Delete
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeleteForm_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync("/ActivityQuestions/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePost_NoAnswers_RedirectsAndRemoves()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion question = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            question = TestData.Question(activity, "A supprimer");
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync($"/ActivityQuestions/Delete/{question.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("ActivityQuestions"); // RedirectToAction(Index)

        using var db = factory.NewDbContext();
        db.ActivityQuestions.Any(q => q.Id == question.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeletePost_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync("/ActivityQuestions/Delete/999999",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // JSON endpoints
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetQuestionsForActivity_ReturnsOnlyActiveQuestions()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            var active = TestData.Question(activity, "QuestionActive", isActive: true, displayOrder: 1);
            var inactive = TestData.Question(activity, "QuestionInactive", isActive: false, displayOrder: 2);
            ctx.AddRange(org, activity, active, inactive);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityQuestions/GetQuestionsForActivity?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("QuestionActive").And.NotContain("QuestionInactive");
    }

    [Fact]
    public async Task ToggleActive_UnknownId_ReturnsJsonFailure()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync("/ActivityQuestions/ToggleActive",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = "999999",
                ["isActive"] = "true"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task ToggleActive_ExistingQuestion_TogglesAndReturnsSuccess()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion question = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            question = TestData.Question(activity, "Bascule", isActive: true);
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync("/ActivityQuestions/ToggleActive",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = question.Id.ToString(),
                ["isActive"] = "false"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");

        using var db = factory.NewDbContext();
        db.ActivityQuestions.Single(q => q.Id == question.Id).IsActive.Should().BeFalse();
    }
}
