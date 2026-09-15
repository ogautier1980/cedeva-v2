using System.Net;
using Autofac;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage tests for <c>ExcursionsController</c>, which is deliberately limited to viewing,
/// editing and deleting an excursion plus announcing it to parents.
///
/// Covers: Index (id, session fallback, NotFound, tenant isolation), Create GET + POST
/// (valid / model-invalid / no-groups / inactive-day), Details, Edit GET + POST
/// (valid / NotFound), Delete GET + POST (with / without registrations), SendEmail GET + POST
/// (recipients, merge variables, payment link, billing idempotence, no-recipients) and
/// BeginExcursions.
/// </summary>
[Collection("WebApp")]
public class ExcursionsControllerCoverageTests
{
    private sealed record Seeded(
        int OrgId,
        int ActivityId,
        int GroupId,
        int ExcursionId,
        int BookingId,
        int TeamMemberId,
        DateTime ActiveDay);

    private const string OtherOrgUser = "u-other";

    /// <summary>
    /// Seeds a full excursion graph: org, activity (with one active ActivityDay matching the
    /// excursion date), one group, parent + child + confirmed booking, an excursion linked to
    /// the group, and a team member assigned to the parent activity.
    /// </summary>
    private static Seeded SeedFull(CedevaWebApplicationFactory factory, decimal cost = 15m, bool excursionActive = true)
    {
        Organisation org = null!;
        Activity activity = null!;
        ActivityGroup group = null!;
        Excursion excursion = null!;
        Booking booking = null!;
        TeamMember teamMember = null!;
        var activeDay = new DateTime(2026, 7, 3); // == TestData.Excursion ExcursionDate

        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org);
            group = TestData.Group(activity, "Lions");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            booking = TestData.Booking(child, activity, group, totalAmount: 100m, paidAmount: 0m);
            excursion = TestData.Excursion(activity, cost, isActive: excursionActive);
            var link = TestData.ExcursionGroup(excursion, group);

            var day = new ActivityDay
            {
                Label = "Jour 1",
                DayDate = activeDay,
                IsActive = true,
                Activity = activity
            };

            // The child attends the excursion's day — that is what makes them part of it.
            booking.Days.Add(new BookingDay { ActivityDay = day, IsReserved = true });

            teamMember = new TeamMember
            {
                FirstName = "Tim",
                LastName = "Team",
                Email = "tim.team@test.be",
                MobilePhoneNumber = "0470111222",
                NationalRegisterNumber = "85061513380",
                BirthDate = new DateTime(1985, 6, 15),
                Address = TestData.Address(),
                TeamRole = TeamRole.Animator,
                License = License.License,
                Status = Status.Volunteer,
                LicenseUrl = "/uploads/seed/license.pdf",
                Organisation = org
            };
            // Assign team member to the parent activity (used by TeamManagement / Assign).
            activity.TeamMembers.Add(teamMember);

            ctx.AddRange(org, activity, group, parent, child, booking, excursion, link, day, teamMember);
            return 0;
        });

        return new Seeded(org.Id, activity.Id, group.Id, excursion.Id, booking.Id, teamMember.TeamMemberId, activeDay);
    }

    /// <summary>Registrations are no longer managed through the UI (sending the excursion e-mail
    /// creates them), so tests that need one seed it directly.</summary>
    private static int RegisterChild(CedevaWebApplicationFactory factory, Seeded s)
    {
        using var ctx = factory.NewDbContext();
        var registration = new ExcursionRegistration
        {
            ExcursionId = s.ExcursionId,
            BookingId = s.BookingId,
            RegistrationDate = DateTime.UtcNow,
        };
        ctx.ExcursionRegistrations.Add(registration);
        ctx.SaveChanges();
        return registration.Id;
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);

    // ---------------------------------------------------------------------
    // Index
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithActivityId_ReturnsExcursionList()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Index/{s.ActivityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Excursion Test");
    }

    [Fact]
    public async Task Index_UsesSessionFallback_WhenNoIdProvided()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        // First request seeds the session with the activity id.
        (await client.GetAsync($"/Excursions/Index/{s.ActivityId}")).EnsureSuccessStatusCode();

        // Second request without an id should fall back to the session value.
        var response = await client.GetAsync("/Excursions/Index");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Excursion Test");
    }

    [Fact]
    public async Task Index_NoIdAndNoSession_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Index");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Index_UnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Index/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Index_ForeignOrganisation_ReturnsNotFound_TenantIsolation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        // Coordinator of a DIFFERENT org cannot see the seeded activity (filtered out).
        var client = factory.CreateClientFor(OtherOrgUser, s.OrgId + 999, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Index/{s.ActivityId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Index_Unauthenticated_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Excursions/Index/{s.ActivityId}");

        // Test auth scheme returns NoResult => challenge => 401 (no cookie redirect configured for Test scheme).
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // Create (GET / POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_Get_ReturnsForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Create/{s.ActivityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_Get_UnknownActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Create/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Post_Valid_PersistsExcursionAndGroupAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Create", Form(new()
        {
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Nouvelle Excursion",
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "12", // whole number avoids fr-vs-invariant decimal-separator ambiguity
            ["Type"] = ExcursionType.Pool.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        // RedirectToAction(Index, new { id = ActivityId }) populates the {id} route segment.
        response.Headers.Location!.ToString().Should().Be($"/Excursions/Index/{s.ActivityId}");

        await using var ctx = factory.NewDbContext();
        var created = await ctx.Excursions.Include(e => e.ExcursionGroups)
            .SingleAsync(e => e.Name == "Nouvelle Excursion");
        created.Cost.Should().Be(12m);
        created.IsActive.Should().BeTrue();
        created.ExcursionGroups.Should().ContainSingle(g => g.ActivityGroupId == s.GroupId);
    }

    [Fact]
    public async Task Create_Post_ModelInvalid_MissingName_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Create", Form(new()
        {
            ["ActivityId"] = s.ActivityId.ToString(),
            // Name omitted -> invalid
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "12.50",
            ["Type"] = ExcursionType.Pool.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var ctx = factory.NewDbContext();
        (await ctx.Excursions.CountAsync(e => e.ActivityId == s.ActivityId)).Should().Be(1); // only the seeded one
    }

    [Fact]
    public async Task Create_Post_NoGroups_ReRendersWith200_AndNothingPersisted()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Create", Form(new()
        {
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Sans Groupe",
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "12.50",
            ["Type"] = ExcursionType.Pool.ToString(),
            // SelectedGroupIds omitted -> "at least one group required"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var ctx = factory.NewDbContext();
        (await ctx.Excursions.AnyAsync(e => e.Name == "Sans Groupe")).Should().BeFalse();
    }

    [Fact]
    public async Task Create_Post_DateNotActiveDay_ReRendersWith200_AndNothingPersisted()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Create", Form(new()
        {
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Mauvais Jour",
            ["ExcursionDate"] = "2030-01-01", // not an active activity day
            ["Cost"] = "12.50",
            ["Type"] = ExcursionType.Pool.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var ctx = factory.NewDbContext();
        (await ctx.Excursions.AnyAsync(e => e.Name == "Mauvais Jour")).Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // Details
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Details_ExistingExcursion_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Details/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Excursion Test");
    }

    [Fact]
    public async Task Details_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_ForeignOrganisation_ReturnsNotFound_TenantIsolation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor(OtherOrgUser, s.OrgId + 999, "Coordinator");

        // Excursion.Activity is a required nav into a tenancy-filtered entity -> filtered out.
        var response = await client.GetAsync($"/Excursions/Details/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // Edit (GET / POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Edit_Get_ReturnsForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Edit/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_Post_Valid_UpdatesAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Edit", Form(new()
        {
            ["Id"] = s.ExcursionId.ToString(),
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Excursion Modifiee",
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "20",
            ["Type"] = ExcursionType.Sports.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be($"/Excursions/Index/{s.ActivityId}");

        await using var ctx = factory.NewDbContext();
        var updated = await ctx.Excursions.SingleAsync(e => e.Id == s.ExcursionId);
        updated.Name.Should().Be("Excursion Modifiee");
        updated.Cost.Should().Be(20m);
        updated.Type.Should().Be(ExcursionType.Sports);
    }

    [Fact]
    public async Task Edit_Post_ModelInvalid_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Edit", Form(new()
        {
            ["Id"] = s.ExcursionId.ToString(),
            ["ActivityId"] = s.ActivityId.ToString(),
            // Name missing -> invalid
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "20",
            ["Type"] = ExcursionType.Sports.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Post_NoGroups_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Edit", Form(new()
        {
            ["Id"] = s.ExcursionId.ToString(),
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Pas De Groupe",
            ["ExcursionDate"] = s.ActiveDay.ToString("yyyy-MM-dd"),
            ["Cost"] = "20",
            ["Type"] = ExcursionType.Sports.ToString(),
            // no SelectedGroupIds
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Post_DateNotActiveDay_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Edit", Form(new()
        {
            ["Id"] = s.ExcursionId.ToString(),
            ["ActivityId"] = s.ActivityId.ToString(),
            ["Name"] = "Mauvaise Date",
            ["ExcursionDate"] = "2031-02-02",
            ["Cost"] = "20",
            ["Type"] = ExcursionType.Sports.ToString(),
            ["SelectedGroupIds"] = s.GroupId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // Delete (GET / POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Delete_Get_ReturnsConfirmation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/Delete/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Post_NoRegistrations_SoftDeletesAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Delete", Form(new()
        {
            ["id"] = s.ExcursionId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be($"/Excursions/Index/{s.ActivityId}");

        await using var ctx = factory.NewDbContext();
        var excursion = await ctx.Excursions.SingleAsync(e => e.Id == s.ExcursionId);
        excursion.IsActive.Should().BeFalse(); // soft delete
    }

    [Fact]
    public async Task Delete_Post_WithRegistrations_DoesNotDelete_AndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        RegisterChild(factory, s); // create a registration that blocks delete

        var response = await client.PostAsync("/Excursions/Delete", Form(new()
        {
            ["id"] = s.ExcursionId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be($"/Excursions/Index/{s.ActivityId}");

        await using var ctx = factory.NewDbContext();
        var excursion = await ctx.Excursions.SingleAsync(e => e.Id == s.ExcursionId);
        excursion.IsActive.Should().BeTrue(); // not deleted because it has registrations
    }

    [Fact]
    public async Task Delete_Post_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/Delete", Form(new()
        {
            ["id"] = "999999",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // SendEmail (GET / POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SendEmail_Get_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync($"/Excursions/SendEmail/{s.ExcursionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendEmail_Get_Unknown_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.GetAsync("/Excursions/SendEmail/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendEmail_Post_WithRecipients_Redirects()
    {
        var fake = new FakeEmailService();
        using var factory = new CedevaWebApplicationFactory
        {
            ConfigureExtraTestContainer = b => b.RegisterInstance(fake).As<IEmailService>()
        };
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/SendEmail", Form(new()
        {
            ["ExcursionId"] = s.ExcursionId.ToString(),
            ["SelectedRecipient"] = "all",
            ["Subject"] = "Info excursion",
            ["Message"] = "Bonjour, voici les details.",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        fake.Sent.Should().ContainSingle("the excursion email must actually be sent, not just counted");
        fake.Sent[0].To.Should().Contain("paul.parent@test.be");
        fake.Sent[0].Html.Should().Contain("/OnlinePayment/Checkout", "the parent needs a way to pay the excursion");

        // Announcing the excursion also bills it: the child is registered and their booking total
        // grew by the excursion cost (100 seeded + 15 excursion).
        await using var ctx = factory.NewDbContext();
        ctx.ExcursionRegistrations.Count(r => r.ExcursionId == s.ExcursionId).Should().Be(1);
        (await ctx.Bookings.IgnoreQueryFilters().SingleAsync(b => b.Id == s.BookingId)).TotalAmount.Should().Be(115m);
    }

    [Fact]
    public async Task SendEmail_Post_Twice_DoesNotBillTheExcursionAgain()
    {
        var fake = new FakeEmailService();
        using var factory = new CedevaWebApplicationFactory
        {
            ConfigureExtraTestContainer = b => b.RegisterInstance(fake).As<IEmailService>()
        };
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        HttpContent Payload() => Form(new()
        {
            ["ExcursionId"] = s.ExcursionId.ToString(),
            ["SelectedRecipient"] = "all",
            ["Subject"] = "Info excursion",
            ["Message"] = "Bonjour.",
        });

        await client.PostAsync("/Excursions/SendEmail", Payload());
        await client.PostAsync("/Excursions/SendEmail", Payload());

        await using var ctx = factory.NewDbContext();
        ctx.ExcursionRegistrations.Count(r => r.ExcursionId == s.ExcursionId).Should().Be(1);
        (await ctx.Bookings.IgnoreQueryFilters().SingleAsync(b => b.Id == s.BookingId)).TotalAmount.Should().Be(115m);
    }

    [Fact]
    public async Task SendEmail_Post_PerChild_ResolvesExcursionAndChildMergeVariables()
    {
        // Regression test (Lot F): %excursion_name%/%excursion_date%/%child_firstname%/
        // %child_lastname% are advertised in the Summernote "Variables" dropdown on this screen but
        // were never actually wired into ReplaceVariables — they used to reach the sent e-mail as
        // literal, unresolved text.
        var fake = new FakeEmailService();
        using var factory = new CedevaWebApplicationFactory
        {
            ConfigureExtraTestContainer = b => b.RegisterInstance(fake).As<IEmailService>()
        };
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/SendEmail", Form(new()
        {
            ["ExcursionId"] = s.ExcursionId.ToString(),
            ["SelectedRecipient"] = "all",
            ["Subject"] = "Excursion : %excursion_name% le %excursion_date%",
            ["Message"] = "Bonjour, %child_firstname% %child_lastname% est invité(e) à %excursion_name%.",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        fake.Sent.Should().ContainSingle();
        // Date separator follows the fr-BE request culture (matches the app's other date merge
        // variables, e.g. %date_debut_activite%) — not necessarily "/".
        var expectedDate = new DateTime(2026, 7, 3).ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("fr-BE"));
        fake.Sent[0].Subject.Should().Be($"Excursion : Excursion Test le {expectedDate}");
        fake.Sent[0].Html.Should().StartWith("Bonjour, Chloé Enfant est invité(e) à Excursion Test.");
    }

    [Fact]
    public async Task SendEmail_Post_ModelInvalid_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/SendEmail", Form(new()
        {
            ["ExcursionId"] = s.ExcursionId.ToString(),
            ["SelectedRecipient"] = "all",
            // Subject + Message missing -> invalid
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendEmail_Post_NoRecipientsFound_ReRendersWith200()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        // Filtering on a group nobody belongs to => no recipients => re-render with model error.
        var response = await client.PostAsync("/Excursions/SendEmail", Form(new()
        {
            ["ExcursionId"] = s.ExcursionId.ToString(),
            ["SelectedRecipient"] = "group_999999",
            ["Subject"] = "Info excursion",
            ["Message"] = "Bonjour.",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // BeginExcursions
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BeginExcursions_Post_RedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        var s = SeedFull(factory);
        var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");

        var response = await client.PostAsync("/Excursions/BeginExcursions", Form(new()
        {
            ["id"] = s.ActivityId.ToString(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        // RedirectToAction(Index, new { id }) => "/Excursions/Index/{id}"
        response.Headers.Location!.ToString().Should().Contain(s.ActivityId.ToString());
    }
}
