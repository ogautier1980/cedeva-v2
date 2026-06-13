using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration tests for ActivityGroupsController. Note: unlike Activity/Parent/Child/etc.,
/// the ActivityGroup entity has NO multi-tenancy query filter and the controller never scopes
/// its queries by organisation, so groups are reachable regardless of the caller's organisation.
/// These tests assert the controller's ACTUAL behaviour.
/// </summary>
[Collection("WebApp")]
public class ActivityGroupsControllerIntegrationTests
{
    private static FormUrlEncodedContent Form(IDictionary<string, string> values) =>
        new(values);

    // ---------------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/ActivityGroups");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePost_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/ActivityGroups/Create", Form(new Dictionary<string, string>
        {
            ["Label"] = "X",
            ["ActivityId"] = "1"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------
    // Index
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Index_WithActivityIdQueryParam_StoresInSessionAndRedirectsToCleanUrl()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            ctx.AddRange(org, a);
            return a;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityGroups?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Found); // 302 -> clean URL
        response.Headers.Location!.ToString().Should().Contain("ActivityGroups");
    }

    [Fact]
    public async Task Index_WithoutQueryParams_RendersGroupsList()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org, "Stage Soleil");
            var g = TestData.Group(a, "GroupeIndex");
            ctx.AddRange(org, a, g);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync("/ActivityGroups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("GroupeIndex");
    }

    // ---------------------------------------------------------------------
    // Create (GET)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreateGet_ReturnsForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            ctx.AddRange(org, a);
            return a;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityGroups/Create?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------
    // Create (POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreatePost_Valid_PersistsGroupAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            ctx.AddRange(org, a);
            return a;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync("/ActivityGroups/Create", Form(new Dictionary<string, string>
        {
            ["Label"] = "Nouveau Groupe",
            ["Capacity"] = "12",
            ["ActivityId"] = activity.Id.ToString()
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("ActivityGroups");

        using var db = factory.NewDbContext();
        var created = await db.ActivityGroups.SingleOrDefaultAsync(g => g.Label == "Nouveau Groupe");
        created.Should().NotBeNull();
        created!.Capacity.Should().Be(12);
        created.ActivityId.Should().Be(activity.Id);
    }

    [Fact]
    public async Task CreatePost_MissingLabel_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        var activity = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            ctx.AddRange(org, a);
            return a;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync("/ActivityGroups/Create", Form(new Dictionary<string, string>
        {
            // Label omitted -> [Required] fails
            ["Capacity"] = "5",
            ["ActivityId"] = activity.Id.ToString()
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-render with ModelState errors

        using var db = factory.NewDbContext();
        (await db.ActivityGroups.CountAsync()).Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // Edit (GET)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EditGet_ExistingGroup_ReturnsForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var group = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            var g = TestData.Group(a, "AModifier");
            ctx.AddRange(org, a, g);
            return g;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityGroups/Edit/{group.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("AModifier");
    }

    [Fact]
    public async Task EditGet_UnknownGroup_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync("/ActivityGroups/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // Edit (POST)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EditPost_Valid_UpdatesGroupAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var seed = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            var g = TestData.Group(a, "AvantEdit");
            ctx.AddRange(org, a, g);
            return new { Group = g, Activity = a };
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync($"/ActivityGroups/Edit/{seed.Group.Id}", Form(new Dictionary<string, string>
        {
            ["Id"] = seed.Group.Id.ToString(),
            ["Label"] = "ApresEdit",
            ["Capacity"] = "30",
            ["ActivityId"] = seed.Activity.Id.ToString()
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var updated = await db.ActivityGroups.SingleAsync(g => g.Id == seed.Group.Id);
        updated.Label.Should().Be("ApresEdit");
        updated.Capacity.Should().Be(30);
    }

    [Fact]
    public async Task EditPost_IdMismatch_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var seed = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            var g = TestData.Group(a, "Mismatch");
            ctx.AddRange(org, a, g);
            return new { Group = g, Activity = a };
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        // Route id differs from model.Id -> NotFound before any persistence.
        var response = await client.PostAsync($"/ActivityGroups/Edit/{seed.Group.Id}", Form(new Dictionary<string, string>
        {
            ["Id"] = (seed.Group.Id + 1000).ToString(),
            ["Label"] = "NeverSaved",
            ["ActivityId"] = seed.Activity.Id.ToString()
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var db = factory.NewDbContext();
        (await db.ActivityGroups.SingleAsync(g => g.Id == seed.Group.Id)).Label.Should().Be("Mismatch");
    }

    [Fact]
    public async Task EditPost_MissingLabel_ReturnsViewAndDoesNotUpdate()
    {
        using var factory = new CedevaWebApplicationFactory();
        var seed = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            var g = TestData.Group(a, "Inchange");
            ctx.AddRange(org, a, g);
            return new { Group = g, Activity = a };
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync($"/ActivityGroups/Edit/{seed.Group.Id}", Form(new Dictionary<string, string>
        {
            ["Id"] = seed.Group.Id.ToString(),
            // Label omitted -> invalid ModelState
            ["ActivityId"] = seed.Activity.Id.ToString()
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        (await db.ActivityGroups.SingleAsync(g => g.Id == seed.Group.Id)).Label.Should().Be("Inchange");
    }

    // ---------------------------------------------------------------------
    // Delete (GET)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeleteGet_ExistingGroup_ReturnsConfirmView()
    {
        using var factory = new CedevaWebApplicationFactory();
        var group = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            var g = TestData.Group(a, "ASupprimer");
            ctx.AddRange(org, a, g);
            return g;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityGroups/Delete/{group.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("ASupprimer");
    }

    [Fact]
    public async Task DeleteGet_UnknownGroup_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.GetAsync("/ActivityGroups/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // Delete (POST / DeleteConfirmed)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeletePost_GroupWithoutBookings_RemovesGroupAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var group = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            var g = TestData.Group(a, "Vide");
            ctx.AddRange(org, a, g);
            return g;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync($"/ActivityGroups/Delete/{group.Id}", Form(new Dictionary<string, string>
        {
            ["id"] = group.Id.ToString()
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("ActivityGroups");

        using var db = factory.NewDbContext();
        (await db.ActivityGroups.AnyAsync(g => g.Id == group.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletePost_GroupWithBookings_DoesNotRemoveAndRedirectsBackToDelete()
    {
        using var factory = new CedevaWebApplicationFactory();
        var group = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            var a = TestData.Activity(org);
            var g = TestData.Group(a, "AvecReservation");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, a, g, totalAmount: 100m, paidAmount: 0m);
            ctx.AddRange(org, a, g, parent, child, booking);
            return g;
        });

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync($"/ActivityGroups/Delete/{group.Id}", Form(new Dictionary<string, string>
        {
            ["id"] = group.Id.ToString()
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Delete");

        using var db = factory.NewDbContext();
        (await db.ActivityGroups.AnyAsync(g => g.Id == group.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task DeletePost_UnknownGroup_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: null, role: "Coordinator");
        var response = await client.PostAsync("/ActivityGroups/Delete/999999", Form(new Dictionary<string, string>
        {
            ["id"] = "999999"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------
    // Multi-tenancy: ActivityGroup is NOT org-filtered, so a coordinator of a
    // DIFFERENT organisation can still reach a group. This documents the
    // controller's actual (unscoped) behaviour.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EditGet_CoordinatorOfOtherOrganisation_CanStillAccessGroup_NotScoped()
    {
        using var factory = new CedevaWebApplicationFactory();
        var group = factory.Seed(ctx =>
        {
            var org = TestData.Organisation("Org A");
            var a = TestData.Activity(org);
            var g = TestData.Group(a, "GroupeOrgA");
            ctx.AddRange(org, a, g);
            return g;
        });

        // Coordinator of an unrelated organisation.
        var client = factory.CreateClientFor("u1", organisationId: 99999, role: "Coordinator");
        var response = await client.GetAsync($"/ActivityGroups/Edit/{group.Id}");

        // ActivityGroups has no query filter and the controller does not scope by org,
        // so the group remains reachable.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
