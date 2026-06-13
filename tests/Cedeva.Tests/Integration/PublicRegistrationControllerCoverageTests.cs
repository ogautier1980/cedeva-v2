using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Additional coverage for
/// <see cref="Cedeva.Website.Features.PublicRegistration.PublicRegistrationController"/>,
/// exercising branches the original integration test left untouched:
/// the GET step-guard redirects of the TempData wizard, the Register GET background-colour
/// branch, the Register POST duplicate / required-question / update-existing branches,
/// and the EmbedCode authorisation edge cases.
/// </summary>
[Collection("WebApp")]
public class PublicRegistrationControllerCoverageTests
{
    private static HttpClient Anonymous(CedevaWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // ---------------------------------------------------------------------
    // Wizard GET step-guards: when the prerequisite TempData keys are absent
    // each intermediate step redirects back to SelectActivity (302).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ParentInformation_Get_NoTempData_RedirectsToSelectActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = Anonymous(factory);
        var response = await client.GetAsync("/PublicRegistration/ParentInformation");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("SelectActivity");
    }

    [Fact]
    public async Task ChildInformation_Get_NoTempData_RedirectsToSelectActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = Anonymous(factory);
        var response = await client.GetAsync("/PublicRegistration/ChildInformation");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("SelectActivity");
    }

    [Fact]
    public async Task ActivityQuestions_Get_NoTempData_RedirectsToSelectActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = Anonymous(factory);
        var response = await client.GetAsync("/PublicRegistration/ActivityQuestions");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("SelectActivity");
    }

    [Fact]
    public async Task CreateBooking_Get_NoTempData_RedirectsToSelectActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = Anonymous(factory);
        var response = await client.GetAsync("/PublicRegistration/CreateBooking");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("SelectActivity");
    }

    // ---------------------------------------------------------------------
    // GET Register: the optional bg query parameter is honoured.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_Get_WithBackgroundColour_AppliesItInTheRenderedPage()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org, "Stage Couleur");
            ctx.AddRange(org, a);
            return a;
        });

        var client = Anonymous(factory);
        var response = await client.GetAsync($"/PublicRegistration/Register?activityId={activity.Id}&bg=abcdef");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("abcdef");
    }

    // ---------------------------------------------------------------------
    // POST Register: duplicate booking re-renders the form (200) without
    // creating a second booking.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_Post_DuplicateBooking_ReturnsOkAndCreatesNoSecondBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Doublon");
            var parent = TestData.Parent(org);
            // Child whose NRN/parent matches what the POST below will resolve to.
            // Register POST compares NRN as submitted (formatted), so store the formatted form.
            var c = TestData.Child(parent);
            c.NationalRegisterNumber = "16.07.08-164.10";
            parent.Email = "doublon.parent@test.be";
            var existing = TestData.Booking(c, activity, group: null, totalAmount: 0m, paidAmount: 0m);
            ctx.AddRange(org, activity, parent, c, existing);
            return 0;
        });

        var client = Anonymous(factory);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = activity.Id.ToString(),
            ["ParentFirstName"] = "Paul",
            ["ParentLastName"] = "Parent",
            ["ParentEmail"] = "doublon.parent@test.be",
            ["ParentPhoneNumber"] = "021234567",
            ["ParentNationalRegisterNumber"] = "85.06.15-133.80",
            ["ParentStreet"] = "Rue Doublon 1",
            ["ParentPostalCode"] = "1000",
            ["ParentCity"] = "Bruxelles",
            ["ChildFirstName"] = "Enzo",
            ["ChildLastName"] = "Enfant",
            ["ChildBirthDate"] = "2016-07-08",
            ["ChildNationalRegisterNumber"] = "16.07.08-164.10",
        });

        var response = await client.PostAsync("/PublicRegistration/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters()
            .Count(b => b.ActivityId == activity.Id)
            .Should().Be(1);
    }

    // ---------------------------------------------------------------------
    // POST Register: an unanswered REQUIRED custom question fails validation
    // and re-renders the form (200) without creating a booking.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_Post_UnansweredRequiredQuestion_ReturnsOkAndCreatesNoBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Question");
            var question = TestData.Question(activity, "Allergies ?", isRequired: true);
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = Anonymous(factory);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = activity.Id.ToString(),
            ["ParentFirstName"] = "Paul",
            ["ParentLastName"] = "Parent",
            ["ParentEmail"] = "question.parent@test.be",
            ["ParentPhoneNumber"] = "021234567",
            ["ParentNationalRegisterNumber"] = "85.06.15-133.80",
            ["ParentStreet"] = "Rue Question 1",
            ["ParentPostalCode"] = "1000",
            ["ParentCity"] = "Bruxelles",
            ["ChildFirstName"] = "Enzo",
            ["ChildLastName"] = "Enfant",
            ["ChildBirthDate"] = "2016-07-08",
            ["ChildNationalRegisterNumber"] = "16.07.08-164.10",
            // The required question answer is intentionally omitted.
        });

        var response = await client.PostAsync("/PublicRegistration/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters()
            .Any(b => b.ActivityId == activity.Id)
            .Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // POST Register: a valid answer to a custom question is persisted with
    // the new booking.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_Post_AnsweredQuestion_PersistsAnswer()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        var question = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Reponse");
            var q = TestData.Question(activity, "Allergies ?", isRequired: true);
            ctx.AddRange(org, activity, q);
            return q;
        });

        var client = Anonymous(factory);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = activity.Id.ToString(),
            ["ParentFirstName"] = "Paul",
            ["ParentLastName"] = "Parent",
            ["ParentEmail"] = "reponse.parent@test.be",
            ["ParentPhoneNumber"] = "021234567",
            ["ParentNationalRegisterNumber"] = "85.06.15-133.80",
            ["ParentStreet"] = "Rue Reponse 1",
            ["ParentPostalCode"] = "1000",
            ["ParentCity"] = "Bruxelles",
            ["ChildFirstName"] = "Enzo",
            ["ChildLastName"] = "Enfant",
            ["ChildBirthDate"] = "2016-07-08",
            ["ChildNationalRegisterNumber"] = "16.07.08-164.10",
            [$"QuestionAnswers[{question.Id}]"] = "Aucune allergie",
        });

        var response = await client.PostAsync("/PublicRegistration/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Confirmation");

        using var db = factory.NewDbContext();
        var answer = db.ActivityQuestionAnswers
            .IgnoreQueryFilters()
            .FirstOrDefault(a => a.ActivityQuestionId == question.Id);
        answer.Should().NotBeNull();
        answer!.AnswerText.Should().Be("Aucune allergie");
    }

    // ---------------------------------------------------------------------
    // POST Register: an existing parent (matched by email + org) is updated
    // rather than duplicated.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_Post_ExistingParentByEmail_UpdatesInsteadOfDuplicating()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        var parent = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Parent Existant");
            var p = TestData.Parent(org);
            p.Email = "existant.parent@test.be";
            p.FirstName = "AncienPrenom";
            ctx.AddRange(org, activity, p);
            return p;
        });

        var client = Anonymous(factory);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = activity.Id.ToString(),
            ["ParentFirstName"] = "NouveauPrenom",
            ["ParentLastName"] = "Parent",
            ["ParentEmail"] = "existant.parent@test.be",
            ["ParentPhoneNumber"] = "021234567",
            ["ParentNationalRegisterNumber"] = "85.06.15-133.80",
            ["ParentStreet"] = "Rue Existant 1",
            ["ParentPostalCode"] = "1000",
            ["ParentCity"] = "Bruxelles",
            ["ChildFirstName"] = "Enzo",
            ["ChildLastName"] = "Enfant",
            ["ChildBirthDate"] = "2016-07-08",
            ["ChildNationalRegisterNumber"] = "16.07.08-164.10",
        });

        var response = await client.PostAsync("/PublicRegistration/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        db.Parents.IgnoreQueryFilters()
            .Count(p => p.Email == "existant.parent@test.be")
            .Should().Be(1);
        db.Parents.IgnoreQueryFilters()
            .First(p => p.Id == parent.Id)
            .FirstName.Should().Be("NouveauPrenom");
    }

    // ---------------------------------------------------------------------
    // POST Register: an existing child (matched by NRN + parent) is updated
    // rather than duplicated.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_Post_ExistingChildByNrn_UpdatesInsteadOfDuplicating()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Enfant Existant");
            var parent = TestData.Parent(org);
            parent.Email = "enfant.parent@test.be";
            child = TestData.Child(parent);
            // Register POST compares NRN as submitted (formatted), so store the formatted form.
            child.NationalRegisterNumber = "16.07.08-164.10";
            child.FirstName = "AncienEnfant";
            ctx.AddRange(org, activity, parent, child);
            return 0;
        });

        var client = Anonymous(factory);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = activity.Id.ToString(),
            ["ParentFirstName"] = "Paul",
            ["ParentLastName"] = "Parent",
            ["ParentEmail"] = "enfant.parent@test.be",
            ["ParentPhoneNumber"] = "021234567",
            ["ParentNationalRegisterNumber"] = "85.06.15-133.80",
            ["ParentStreet"] = "Rue Enfant 1",
            ["ParentPostalCode"] = "1000",
            ["ParentCity"] = "Bruxelles",
            ["ChildFirstName"] = "NouvelEnfant",
            ["ChildLastName"] = "Enfant",
            ["ChildBirthDate"] = "2016-07-08",
            ["ChildNationalRegisterNumber"] = "16.07.08-164.10",
        });

        var response = await client.PostAsync("/PublicRegistration/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        db.Children.IgnoreQueryFilters()
            .Count(c => c.NationalRegisterNumber == "16.07.08-164.10")
            .Should().Be(1);
        db.Children.IgnoreQueryFilters()
            .First(c => c.Id == child.Id)
            .FirstName.Should().Be("NouvelEnfant");
    }

    // ---------------------------------------------------------------------
    // POST Register: unknown activity (passes model validation but resolves
    // to no activity) returns NotFound.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_Post_UnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = Anonymous(factory);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ActivityId"] = "999999",
            ["ParentFirstName"] = "Paul",
            ["ParentLastName"] = "Parent",
            ["ParentEmail"] = "unknown.parent@test.be",
            ["ParentPhoneNumber"] = "021234567",
            ["ParentNationalRegisterNumber"] = "85.06.15-133.80",
            ["ParentStreet"] = "Rue Inconnue 1",
            ["ParentPostalCode"] = "1000",
            ["ParentCity"] = "Bruxelles",
            ["ChildFirstName"] = "Enzo",
            ["ChildLastName"] = "Enfant",
            ["ChildBirthDate"] = "2016-07-08",
            ["ChildNationalRegisterNumber"] = "16.07.08-164.10",
        });

        var response = await client.PostAsync("/PublicRegistration/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // EmbedCode: authorisation edge cases.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EmbedCode_AuthorisedCoordinator_UnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/PublicRegistration/EmbedCode/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmbedCode_AuthorisedAdmin_ExistingActivity_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Embed Admin");
            ctx.AddRange(org, activity);
            return 0;
        });

        // Admin bypasses the tenancy filter, so no organisation id is required.
        var client = factory.CreateClientFor("admin", null, "Admin");
        var response = await client.GetAsync($"/PublicRegistration/EmbedCode/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
