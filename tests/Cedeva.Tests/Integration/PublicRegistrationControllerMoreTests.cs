using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Further coverage for
/// <see cref="Cedeva.Website.Features.PublicRegistration.PublicRegistrationController"/>,
/// targeting branches the two existing test classes leave uncovered:
/// the full multi-step TempData wizard (SelectActivity -> ParentInformation ->
/// ChildInformation -> ActivityQuestions -> CreateBooking -> Confirmation), the
/// intermediate-step GET renders WHEN the prerequisite TempData IS present, the POST
/// invalid / postal-code / duplicate branches of the wizard, and EmbedCode role denial.
///
/// The wizard relies on TempData flowing across redirects via the TempData cookie. The
/// WebApplicationFactory HttpClient keeps a cookie container, so the steps are driven in
/// order on the SAME client with AllowAutoRedirect=false and each 302 followed manually.
/// </summary>
[Collection("WebApp")]
public class PublicRegistrationControllerMoreTests
{
    private static HttpClient Anonymous(CedevaWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // Valid Belgian NRNs (pass [ValidNationalRegisterNumber]).
    private const string ParentNrn = "85.06.15-133.80";
    private const string ChildNrn = "16.07.08-164.10";

    private static FormUrlEncodedContent Form(Dictionary<string, string> fields) =>
        new(fields);

    // =====================================================================
    // SelectActivity POST
    // =====================================================================

    [Fact]
    public async Task SelectActivity_Post_ValidActivityId_RedirectsToParentInformation()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Wizard");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        // Prime the OrganisationId TempData via the GET first (mirrors the real embed entry).
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");

        var response = await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("ParentInformation");
    }

    [Fact]
    public async Task SelectActivity_Post_MissingActivityId_ReturnsOkReRender()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = Anonymous(factory);
        // No ActivityId => [Required] int? fails => view re-rendered (200).
        var response = await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =====================================================================
    // Intermediate GET renders WHEN prerequisite TempData IS present.
    // =====================================================================

    [Fact]
    public async Task ParentInformation_Get_WithActivityTempData_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Parent Get");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));

        var response = await client.GetAsync("/PublicRegistration/ParentInformation");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChildInformation_Get_WithTempData_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Child Get");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        await DriveToChildInformationGetAsync(client, org, activity);

        var response = await client.GetAsync("/PublicRegistration/ChildInformation");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =====================================================================
    // ParentInformation POST: invalid + postal-code branches.
    // =====================================================================

    [Fact]
    public async Task ParentInformation_Post_InvalidModel_ReturnsOkAndCreatesNoParent()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Parent Invalide");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));

        // Empty parent form: all [Required] fields missing => ModelState invalid => 200.
        var response = await client.PostAsync("/PublicRegistration/ParentInformation",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Parents.IgnoreQueryFilters().Any().Should().BeFalse();
    }

    [Fact]
    public async Task ParentInformation_Post_PostalCodeNotInIncludedList_ReturnsOkAndCreatesNoParent()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Inclusion");
            activity.IncludedPostalCodes = "4000,4020"; // Liège only
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));

        // Postal code 1000 is NOT in the inclusion list => validation error => 200.
        var response = await client.PostAsync("/PublicRegistration/ParentInformation",
            ParentForm(activity.Id, postalCode: "1000"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Parents.IgnoreQueryFilters().Any().Should().BeFalse();
    }

    [Fact]
    public async Task ParentInformation_Post_ExcludedPostalCode_ReturnsOkAndCreatesNoParent()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Exclusion");
            activity.ExcludedPostalCodes = "1000,1030";
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));

        var response = await client.PostAsync("/PublicRegistration/ParentInformation",
            ParentForm(activity.Id, postalCode: "1000"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Parents.IgnoreQueryFilters().Any().Should().BeFalse();
    }

    [Fact]
    public async Task ParentInformation_Post_Valid_CreatesParentAndRedirectsToChildInformation()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Parent Valide");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));

        var response = await client.PostAsync("/PublicRegistration/ParentInformation",
            ParentForm(activity.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("ChildInformation");

        using var db = factory.NewDbContext();
        db.Parents.IgnoreQueryFilters()
            .Any(p => p.Email == "wizard.parent@test.be" && p.OrganisationId == org.Id)
            .Should().BeTrue();
    }

    // =====================================================================
    // ChildInformation POST: invalid + new + existing-update branches.
    // =====================================================================

    [Fact]
    public async Task ChildInformation_Post_InvalidModel_ReturnsOkAndCreatesNoChild()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Enfant Invalide");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        var parentId = await DriveToChildInformationPostAsync(client, factory, org, activity);

        // Empty child form (only the carried Ids) => required fields missing => 200.
        var response = await client.PostAsync("/PublicRegistration/ChildInformation",
            Form(new()
            {
                ["ActivityId"] = activity.Id.ToString(),
                ["ParentId"] = parentId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Children.IgnoreQueryFilters().Any().Should().BeFalse();
    }

    [Fact]
    public async Task ChildInformation_Post_NewChild_CreatesChildAndRedirectsToActivityQuestions()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Enfant Nouveau");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        var parentId = await DriveToChildInformationPostAsync(client, factory, org, activity);

        var response = await client.PostAsync("/PublicRegistration/ChildInformation",
            ChildForm(activity.Id, parentId));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("ActivityQuestions");

        using var db = factory.NewDbContext();
        // NRN is stored stripped of formatting for new children.
        db.Children.IgnoreQueryFilters()
            .Any(c => c.NationalRegisterNumber == "16070816410" && c.ParentId == parentId)
            .Should().BeTrue();
    }

    [Fact]
    public async Task ChildInformation_Post_ExistingChild_UpdatesInsteadOfDuplicating()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Enfant MAJ");
            parent = TestData.Parent(org);
            parent.Email = "wizard.parent@test.be";
            // The wizard child lookup matches on the NRN exactly as submitted (NOT stripped),
            // so seed the child with the formatted NRN to hit the "existing child" branch.
            var child = TestData.Child(parent);
            child.NationalRegisterNumber = ChildNrn;
            child.FirstName = "AncienEnfant";
            ctx.AddRange(org, activity, parent, child);
            return 0;
        });

        var client = Anonymous(factory);
        // Drive through ParentInformation so the existing parent (same email) is reused.
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));
        await client.PostAsync("/PublicRegistration/ParentInformation", ParentForm(activity.Id));
        await client.GetAsync("/PublicRegistration/ChildInformation");

        var response = await client.PostAsync("/PublicRegistration/ChildInformation",
            ChildForm(activity.Id, parent.Id, firstName: "NouvelEnfant"));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        db.Children.IgnoreQueryFilters()
            .Count(c => c.NationalRegisterNumber == ChildNrn && c.ParentId == parent.Id)
            .Should().Be(1);
        db.Children.IgnoreQueryFilters()
            .First(c => c.NationalRegisterNumber == ChildNrn && c.ParentId == parent.Id)
            .FirstName.Should().Be("NouvelEnfant");
    }

    // =====================================================================
    // ActivityQuestions GET: no questions skips straight to CreateBooking.
    // =====================================================================

    [Fact]
    public async Task ActivityQuestions_Get_NoQuestions_RedirectsToCreateBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Sans Question");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);
        await DriveToActivityQuestionsGetAsync(client, factory, org, activity);

        var response = await client.GetAsync("/PublicRegistration/ActivityQuestions");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("CreateBooking");
    }

    [Fact]
    public async Task ActivityQuestions_Get_WithQuestions_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Avec Question");
            var q = TestData.Question(activity, "Allergies ?", isRequired: false);
            ctx.AddRange(org, activity, q);
            return 0;
        });

        var client = Anonymous(factory);
        await DriveToActivityQuestionsGetAsync(client, factory, org, activity);

        var response = await client.GetAsync("/PublicRegistration/ActivityQuestions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =====================================================================
    // ActivityQuestions POST: missing required + valid answer branches.
    // =====================================================================

    [Fact]
    public async Task ActivityQuestions_Post_MissingRequired_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        ActivityQuestion question = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Question Req");
            question = TestData.Question(activity, "Allergies ?", isRequired: true);
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = Anonymous(factory);
        await DriveToActivityQuestionsGetAsync(client, factory, org, activity);
        await client.GetAsync("/PublicRegistration/ActivityQuestions");

        // Submit with the required answer blank => ModelState error => 200.
        var response = await client.PostAsync("/PublicRegistration/ActivityQuestions",
            Form(new()
            {
                ["ActivityId"] = activity.Id.ToString(),
                [$"Answers[{question.Id}]"] = "",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ActivityQuestions_Post_ValidAnswer_RedirectsToCreateBookingAndPersistsAnswer()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        ActivityQuestion question = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Question OK");
            question = TestData.Question(activity, "Allergies ?", isRequired: true);
            ctx.AddRange(org, activity, question);
            return 0;
        });

        var client = Anonymous(factory);
        await DriveToActivityQuestionsGetAsync(client, factory, org, activity);
        await client.GetAsync("/PublicRegistration/ActivityQuestions");

        var post = await client.PostAsync("/PublicRegistration/ActivityQuestions",
            Form(new()
            {
                ["ActivityId"] = activity.Id.ToString(),
                [$"Answers[{question.Id}]"] = "Aucune",
            }));

        post.StatusCode.Should().Be(HttpStatusCode.Found);
        post.Headers.Location!.ToString().Should().Contain("CreateBooking");

        // Follow CreateBooking to flush the answer (saved from TempData on booking creation).
        var createBooking = await client.GetAsync(post.Headers.Location!.ToString());
        createBooking.StatusCode.Should().Be(HttpStatusCode.Found);
        createBooking.Headers.Location!.ToString().Should().Contain("Confirmation");

        using var db = factory.NewDbContext();
        var answer = db.ActivityQuestionAnswers
            .IgnoreQueryFilters()
            .FirstOrDefault(a => a.ActivityQuestionId == question.Id);
        answer.Should().NotBeNull();
        answer!.AnswerText.Should().Be("Aucune");
    }

    // =====================================================================
    // Full wizard happy path (no questions) end-to-end, following every 302.
    // =====================================================================

    [Fact]
    public async Task FullWizard_NoQuestions_CreatesBookingAndReachesConfirmation()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Complet");
            ctx.AddRange(org, activity);
            return 0;
        });

        var client = Anonymous(factory);

        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");

        var afterSelect = await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));
        afterSelect.Headers.Location!.ToString().Should().Contain("ParentInformation");
        await client.GetAsync(afterSelect.Headers.Location!.ToString());

        var afterParent = await client.PostAsync("/PublicRegistration/ParentInformation",
            ParentForm(activity.Id));
        afterParent.Headers.Location!.ToString().Should().Contain("ChildInformation");
        await client.GetAsync(afterParent.Headers.Location!.ToString());

        var parentId = NewestParentId(factory);

        var afterChild = await client.PostAsync("/PublicRegistration/ChildInformation",
            ChildForm(activity.Id, parentId));
        afterChild.Headers.Location!.ToString().Should().Contain("ActivityQuestions");

        // No questions => ActivityQuestions GET redirects to CreateBooking.
        var afterQuestions = await client.GetAsync(afterChild.Headers.Location!.ToString());
        afterQuestions.StatusCode.Should().Be(HttpStatusCode.Found);
        afterQuestions.Headers.Location!.ToString().Should().Contain("CreateBooking");

        var afterCreate = await client.GetAsync(afterQuestions.Headers.Location!.ToString());
        afterCreate.StatusCode.Should().Be(HttpStatusCode.Found);
        afterCreate.Headers.Location!.ToString().Should().Contain("Confirmation");

        var confirmation = await client.GetAsync(afterCreate.Headers.Location!.ToString());
        confirmation.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await confirmation.Content.ReadAsStringAsync();
        html.Should().Contain("Enfant"); // child last name (ASCII)

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters()
            .Any(b => b.ActivityId == activity.Id)
            .Should().BeTrue();
    }

    // =====================================================================
    // CreateBooking: duplicate booking redirects back to SelectActivity.
    // =====================================================================

    [Fact]
    public async Task CreateBooking_DuplicateBooking_RedirectsToSelectActivityWithoutSecondBooking()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        Parent parent = null!;
        Child child = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Doublon Wizard");
            parent = TestData.Parent(org);
            parent.Email = "wizard.parent@test.be";
            child = TestData.Child(parent);
            // Wizard child lookup matches the submitted (formatted) NRN exactly.
            child.NationalRegisterNumber = ChildNrn;
            var existing = TestData.Booking(child, activity, group: null, totalAmount: 0m, paidAmount: 0m);
            ctx.AddRange(org, activity, parent, child, existing);
            return 0;
        });

        var client = Anonymous(factory);
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));
        await client.PostAsync("/PublicRegistration/ParentInformation", ParentForm(activity.Id));
        await client.GetAsync("/PublicRegistration/ChildInformation");
        // Child update of the already-booked child (same NRN+parent) -> ActivityQuestions.
        var afterChild = await client.PostAsync("/PublicRegistration/ChildInformation",
            ChildForm(activity.Id, parent.Id));
        afterChild.StatusCode.Should().Be(HttpStatusCode.Found);

        // No questions => GET ActivityQuestions redirects to CreateBooking.
        var afterQuestions = await client.GetAsync("/PublicRegistration/ActivityQuestions");
        afterQuestions.Headers.Location!.ToString().Should().Contain("CreateBooking");

        var createBooking = await client.GetAsync(afterQuestions.Headers.Location!.ToString());

        // Duplicate detected => redirect back to SelectActivity, no second booking.
        createBooking.StatusCode.Should().Be(HttpStatusCode.Found);
        createBooking.Headers.Location!.ToString().Should().Contain("SelectActivity");

        using var db = factory.NewDbContext();
        db.Bookings.IgnoreQueryFilters()
            .Count(b => b.ActivityId == activity.Id && b.ChildId == child.Id)
            .Should().Be(1);
    }

    // =====================================================================
    // EmbedCode: authenticated user WITHOUT an allowed role is forbidden.
    // =====================================================================

    [Fact]
    public async Task EmbedCode_AuthenticatedNonPrivilegedRole_ReturnsForbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Role");
            ctx.AddRange(org, activity);
            return 0;
        });

        // "Parent" is not in the [Authorize(Roles="Admin,Coordinator")] list.
        var client = factory.CreateClientFor("u1", org.Id, "Parent");
        var response = await client.GetAsync($"/PublicRegistration/EmbedCode/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static FormUrlEncodedContent ParentForm(int activityId, string postalCode = "1000") =>
        Form(new()
        {
            ["ActivityId"] = activityId.ToString(),
            ["FirstName"] = "Paul",
            ["LastName"] = "Parent",
            ["Email"] = "wizard.parent@test.be",
            ["PhoneNumber"] = "021234567",
            ["MobilePhoneNumber"] = "0470000000",
            ["NationalRegisterNumber"] = ParentNrn,
            ["Street"] = "Rue Wizard 1",
            ["PostalCode"] = postalCode,
            ["City"] = "Bruxelles",
        });

    private static FormUrlEncodedContent ChildForm(int activityId, int parentId,
        string firstName = "Enzo") =>
        Form(new()
        {
            ["ActivityId"] = activityId.ToString(),
            ["ParentId"] = parentId.ToString(),
            ["FirstName"] = firstName,
            ["LastName"] = "Enfant",
            ["BirthDate"] = "2016-07-08",
            ["NationalRegisterNumber"] = ChildNrn,
        });

    /// <summary>Drives SelectActivity GET+POST then ParentInformation POST; returns the parent id.</summary>
    private static async Task<int> DriveToChildInformationPostAsync(
        HttpClient client, CedevaWebApplicationFactory factory, Organisation org, Activity activity)
    {
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));
        var afterParent = await client.PostAsync("/PublicRegistration/ParentInformation",
            ParentForm(activity.Id));
        afterParent.StatusCode.Should().Be(HttpStatusCode.Found);
        await client.GetAsync("/PublicRegistration/ChildInformation");
        return NewestParentId(factory);
    }

    /// <summary>Drives the wizard up to (and including) ChildInformation GET-ready state.</summary>
    private static async Task DriveToChildInformationGetAsync(
        HttpClient client, Organisation org, Activity activity)
    {
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));
        await client.PostAsync("/PublicRegistration/ParentInformation", ParentForm(activity.Id));
    }

    /// <summary>Drives the wizard up to the point where ActivityQuestions GET is reachable.</summary>
    private static async Task DriveToActivityQuestionsGetAsync(
        HttpClient client, CedevaWebApplicationFactory factory, Organisation org, Activity activity)
    {
        await client.GetAsync($"/PublicRegistration/SelectActivity?orgId={org.Id}");
        await client.PostAsync("/PublicRegistration/SelectActivity",
            Form(new() { ["ActivityId"] = activity.Id.ToString() }));
        await client.PostAsync("/PublicRegistration/ParentInformation", ParentForm(activity.Id));
        await client.GetAsync("/PublicRegistration/ChildInformation");
        await client.PostAsync("/PublicRegistration/ChildInformation",
            ChildForm(activity.Id, NewestParentId(factory)));
    }

    private static int NewestParentId(CedevaWebApplicationFactory factory)
    {
        using var db = factory.NewDbContext();
        return db.Parents.IgnoreQueryFilters().OrderByDescending(p => p.Id).First().Id;
    }
}
