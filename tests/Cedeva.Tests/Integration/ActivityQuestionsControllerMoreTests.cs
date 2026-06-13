using System.Net;
using System.Net.Http.Json;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Supplementary coverage for <c>ActivityQuestionsController</c> focused on the JSON/AJAX
/// endpoints and branches not exercised by ActivityQuestionsControllerIntegrationTests:
/// UpdateOrder, ImportQuestions, GetActivitiesWithQuestions, GetQuestionsForActivity edge
/// cases, ToggleActive (activate path), the OptionsRequired branch on Edit, and the
/// Delete-with-answers branch.
/// </summary>
[Collection("WebApp")]
public class ActivityQuestionsControllerMoreTests
{
    // ---------------------------------------------------------------------
    // UpdateOrder (POST, [FromBody] List<QuestionOrderDto>) -> Json
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdateOrder_ValidSameActivity_ReturnsSuccessAndPersists()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion q1 = null!;
        ActivityQuestion q2 = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            q1 = TestData.Question(activity, "Q1", displayOrder: 1);
            q2 = TestData.Question(activity, "Q2", displayOrder: 2);
            ctx.AddRange(org, activity, q1, q2);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var payload = new[]
        {
            new { Id = q1.Id, DisplayOrder = 5 },
            new { Id = q2.Id, DisplayOrder = 6 },
        };

        var response = await client.PostAsJsonAsync("/ActivityQuestions/UpdateOrder", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");

        using var db = factory.NewDbContext();
        db.ActivityQuestions.Single(q => q.Id == q1.Id).DisplayOrder.Should().Be(5);
        db.ActivityQuestions.Single(q => q.Id == q2.Id).DisplayOrder.Should().Be(6);
    }

    [Fact]
    public async Task UpdateOrder_EmptyList_ReturnsJsonFailure()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityQuestions/UpdateOrder",
            Array.Empty<object>());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task UpdateOrder_UnknownIds_ReturnsJsonFailure_NotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var payload = new[] { new { Id = 999999, DisplayOrder = 1 } };

        var response = await client.PostAsJsonAsync("/ActivityQuestions/UpdateOrder", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        // questions.Count (0) != updates.Count (1) -> NotFound failure branch.
        json.Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task UpdateOrder_QuestionsFromTwoActivities_ReturnsJsonFailure_InvalidOperation()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion qA = null!;
        ActivityQuestion qB = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activityA = TestData.Activity(org, "Activite A");
            var activityB = TestData.Activity(org, "Activite B");
            qA = TestData.Question(activityA, "QA", displayOrder: 1);
            qB = TestData.Question(activityB, "QB", displayOrder: 1);
            ctx.AddRange(org, activityA, activityB, qA, qB);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var payload = new[]
        {
            new { Id = qA.Id, DisplayOrder = 2 },
            new { Id = qB.Id, DisplayOrder = 3 },
        };

        var response = await client.PostAsJsonAsync("/ActivityQuestions/UpdateOrder", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        // Distinct activity ids > 1 -> cross-activity rejection branch.
        json.Should().Contain("\"success\":false");

        // Nothing should have been reordered.
        using var db = factory.NewDbContext();
        db.ActivityQuestions.Single(q => q.Id == qA.Id).DisplayOrder.Should().Be(1);
        db.ActivityQuestions.Single(q => q.Id == qB.Id).DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task UpdateOrder_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync("/ActivityQuestions/UpdateOrder",
            new[] { new { Id = 1, DisplayOrder = 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // GetActivitiesWithQuestions (GET) -> Json
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetActivitiesWithQuestions_ReturnsOnlyActivitiesThatHaveQuestions()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var withQuestions = TestData.Activity(org, "Avec Questions");
            var withoutQuestions = TestData.Activity(org, "Sans Questions");
            var q = TestData.Question(withQuestions, "Une question");
            ctx.AddRange(org, withQuestions, withoutQuestions, q);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");
        var response = await client.GetAsync("/ActivityQuestions/GetActivitiesWithQuestions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");
        json.Should().Contain("Avec Questions");
        json.Should().NotContain("Sans Questions");
    }

    [Fact]
    public async Task GetActivitiesWithQuestions_ExcludesCurrentActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity current = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            current = TestData.Activity(org, "Activite Courante");
            var other = TestData.Activity(org, "Autre Activite");
            var q1 = TestData.Question(current, "Q sur courante");
            var q2 = TestData.Question(other, "Q sur autre");
            ctx.AddRange(org, current, other, q1, q2);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");
        var response = await client.GetAsync(
            $"/ActivityQuestions/GetActivitiesWithQuestions?currentActivityId={current.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");
        json.Should().Contain("Autre Activite");
        json.Should().NotContain("Activite Courante");
    }

    [Fact]
    public async Task GetActivitiesWithQuestions_TenantIsolation_OtherOrgSeesNothing()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org, "Org A Activite");
            var q = TestData.Question(activity, "Q org A");
            ctx.AddRange(org, activity, q);
            return 0;
        });

        // A coordinator from an unrelated org id should see no activities (query filter on Organisation).
        var client = factory.CreateClientFor("u1", organisationId: 987654, role: "Coordinator");
        var response = await client.GetAsync("/ActivityQuestions/GetActivitiesWithQuestions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");
        json.Should().NotContain("Org A Activite");
    }

    // ---------------------------------------------------------------------
    // GetQuestionsForActivity (GET) -> Json edge cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetQuestionsForActivity_UnknownActivity_ReturnsEmptySuccess()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync("/ActivityQuestions/GetQuestionsForActivity?activityId=999999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"questions\":[]");
    }

    [Fact]
    public async Task GetQuestionsForActivity_OrdersByDisplayOrder()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            var second = TestData.Question(activity, "ZZZ deuxieme", displayOrder: 2);
            var first = TestData.Question(activity, "AAA premiere", displayOrder: 1);
            ctx.AddRange(org, activity, second, first);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync(
            $"/ActivityQuestions/GetQuestionsForActivity?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.IndexOf("AAA premiere", StringComparison.Ordinal)
            .Should().BeLessThan(json.IndexOf("ZZZ deuxieme", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // ToggleActive: activate (true) path
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ToggleActive_Activate_TogglesAndReturnsSuccess()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion question = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            question = TestData.Question(activity, "Inactif a activer", isActive: false);
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync("/ActivityQuestions/ToggleActive",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = question.Id.ToString(),
                ["isActive"] = "true"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");

        using var db = factory.NewDbContext();
        db.ActivityQuestions.Single(q => q.Id == question.Id).IsActive.Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // ImportQuestions (POST, [FromBody] ImportQuestionsRequest) -> Json
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ImportQuestions_WithSessionTarget_CopiesSelectedQuestions()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity target = null!;
        Activity source = null!;
        ActivityQuestion sourceQuestion = null!;
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            target = TestData.Activity(org, "Activite Cible");
            source = TestData.Activity(org, "Activite Source");
            sourceQuestion = TestData.Question(source, "Question a importer", displayOrder: 1);
            ctx.AddRange(org, target, source, sourceQuestion);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");

        // Seed the session ActivityId (the import target) via the Index redirect branch.
        var seedSession = await client.GetAsync($"/ActivityQuestions?activityId={target.Id}");
        seedSession.StatusCode.Should().Be(HttpStatusCode.Found);

        var response = await client.PostAsJsonAsync("/ActivityQuestions/ImportQuestions",
            new { QuestionIds = new[] { sourceQuestion.Id } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true");

        using var db = factory.NewDbContext();
        var copied = db.ActivityQuestions
            .Where(q => q.ActivityId == target.Id && q.QuestionText == "Question a importer")
            .ToList();
        copied.Should().HaveCount(1);
        copied[0].IsActive.Should().BeTrue();
        // Original source question is untouched.
        db.ActivityQuestions.Count(q => q.QuestionText == "Question a importer").Should().Be(2);
    }

    [Fact]
    public async Task ImportQuestions_EmptyQuestionIds_ReturnsJsonFailure()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity target = null!;
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            target = TestData.Activity(org, "Cible");
            ctx.AddRange(org, target);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");
        (await client.GetAsync($"/ActivityQuestions?activityId={target.Id}")).StatusCode
            .Should().Be(HttpStatusCode.Found);

        var response = await client.PostAsJsonAsync("/ActivityQuestions/ImportQuestions",
            new { QuestionIds = Array.Empty<int>() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        // request.QuestionIds empty -> NoQuestionsSelected failure branch.
        json.Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task ImportQuestions_NoSessionTarget_ReturnsJsonFailure()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion sourceQuestion = null!;
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var source = TestData.Activity(org, "Source");
            sourceQuestion = TestData.Question(source, "Q source");
            ctx.AddRange(org, source, sourceQuestion);
            return 0;
        });

        // Fresh client: no Index request was made, so the session ActivityId is absent.
        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");
        var response = await client.PostAsJsonAsync("/ActivityQuestions/ImportQuestions",
            new { QuestionIds = new[] { sourceQuestion.Id } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        // targetActivityId has no value -> InvalidData failure branch.
        json.Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task ImportQuestions_UnknownSourceIds_ReturnsJsonFailure_NotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity target = null!;
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            target = TestData.Activity(org, "Cible");
            ctx.AddRange(org, target);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");
        (await client.GetAsync($"/ActivityQuestions?activityId={target.Id}")).StatusCode
            .Should().Be(HttpStatusCode.Found);

        var response = await client.PostAsJsonAsync("/ActivityQuestions/ImportQuestions",
            new { QuestionIds = new[] { 999999 } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        // sourceQuestions empty -> NotFound failure branch.
        json.Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task ImportQuestions_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync("/ActivityQuestions/ImportQuestions",
            new { QuestionIds = new[] { 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // Edit POST: OptionsRequired branch (Radio/Dropdown without options)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EditPost_RadioWithoutOptions_ReturnsViewAndDoesNotUpdate()
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
        var form = new Dictionary<string, string>
        {
            ["Id"] = question.Id.ToString(),
            ["ActivityId"] = activity.Id.ToString(),
            ["QuestionText"] = "Choix sans options",
            ["QuestionType"] = ((int)QuestionType.Radio).ToString(),
            ["IsRequired"] = "false",
            // No Options -> server-side OptionsRequired validation on Edit.
        };

        var response = await client.PostAsync($"/ActivityQuestions/Edit/{question.Id}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        var unchanged = db.ActivityQuestions.Single(q => q.Id == question.Id);
        unchanged.QuestionText.Should().Be("Texte original");
        unchanged.QuestionType.Should().Be(QuestionType.Text);
    }

    [Fact]
    public async Task EditPost_WithReturnUrl_RedirectsToReturnUrl()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion question = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org);
            question = TestData.Question(activity, "Avant");
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var form = new Dictionary<string, string>
        {
            ["Id"] = question.Id.ToString(),
            ["ActivityId"] = activity.Id.ToString(),
            ["QuestionText"] = "Apres",
            ["QuestionType"] = ((int)QuestionType.Text).ToString(),
            ["IsRequired"] = "false",
        };

        var response = await client.PostAsync(
            $"/ActivityQuestions/Edit/{question.Id}?returnUrl=/ActivityManagement",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("ActivityManagement");

        using var db = factory.NewDbContext();
        db.ActivityQuestions.Single(q => q.Id == question.Id).QuestionText.Should().Be("Apres");
    }

    // ---------------------------------------------------------------------
    // Delete POST: question that has answers -> blocked
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeletePost_WithAnswers_DoesNotRemoveAndRedirectsBackToDelete()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion question = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var activity = TestData.Activity(org);
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, totalAmount: 100m, paidAmount: 0m);
            question = TestData.Question(activity, "Question avec reponse");
            var answer = new ActivityQuestionAnswer
            {
                ActivityQuestion = question,
                Booking = booking,
                AnswerText = "Une reponse"
            };
            ctx.AddRange(org, activity, parent, child, booking, question, answer);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync($"/ActivityQuestions/Delete/{question.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        // Redirects back to the Delete GET page, not Index, when answers exist.
        response.Headers.Location!.ToString().Should().Contain("Delete");

        using var db = factory.NewDbContext();
        db.ActivityQuestions.Any(q => q.Id == question.Id).Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // GET Delete: renders for existing question
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeleteForm_ExistingQuestion_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        ActivityQuestion question = null!;
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org);
            question = TestData.Question(activity, "A confirmer suppression");
            ctx.AddRange(org, activity, question);
            return 0;
        });

        // Delete GET Includes the tenancy-filtered Activity, so the coordinator must own its org.
        var client = factory.CreateClientFor("u1", organisationId: org.Id, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityQuestions/Delete/{question.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("A confirmer suppression");
    }
}
