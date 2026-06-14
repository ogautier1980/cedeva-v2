using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class TeamMembersControllerIntegrationTests
{
    // Valid Belgian national register numbers (pass the modulo-97 check).
    private const string ValidNrnFormatted = "85.06.15-133.80";
    private const string ValidNrnStored = "85061513380";

    private static TeamMember SeedTeamMember(CedevaDbContext ctx, Organisation org,
        string firstName = "Tom", string lastName = "Animateur",
        string email = "tom.animateur@test.be")
    {
        var address = new Address
        {
            Street = "Rue du Stage",
            City = "Namur",
            PostalCode = "5000",
            Country = Country.Belgium
        };

        var teamMember = new TeamMember
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            MobilePhoneNumber = "0470123456",
            NationalRegisterNumber = ValidNrnStored,
            BirthDate = new DateTime(1985, 6, 15),
            Address = address,
            TeamRole = TeamRole.Animator,
            License = License.License,
            Status = Status.Volunteer,
            // LicenseUrl is configured IsRequired() (NOT NULL) in TeamMemberConfiguration.
            LicenseUrl = "/uploads/seed/license.pdf",
            Organisation = org
        };

        ctx.AddRange(address, teamMember);
        return teamMember;
    }

    private static Dictionary<string, string> ValidCreateForm(int organisationId) => new()
    {
        ["FirstName"] = "Nouvelle",
        ["LastName"] = "Recrue",
        ["Email"] = "nouvelle.recrue@test.be",
        ["MobilePhoneNumber"] = "0470123456",
        ["NationalRegisterNumber"] = ValidNrnFormatted,
        ["BirthDate"] = "1985-06-15",
        ["Street"] = "Rue Neuve",
        ["City"] = "Liège",
        ["PostalCode"] = "4000",
        ["Country"] = Country.Belgium.ToString(),
        ["TeamRole"] = TeamRole.Animator.ToString(),
        ["License"] = License.License.ToString(),
        ["Status"] = Status.Volunteer.ToString(),
        ["OrganisationId"] = organisationId.ToString()
    };

    // ---------------- Index ----------------

    [Fact]
    public async Task Index_AuthenticatedUser_ReturnsOkWithMembers()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            SeedTeamMember(ctx, org, lastName: "Dupont");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/TeamMembers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Dupont");
    }

    [Fact]
    public async Task Index_OnlyShowsMembersOfOwnOrganisation()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation orgA = null!;
        Organisation orgB = null!;
        factory.Seed(ctx =>
        {
            orgA = TestData.Organisation("Org A");
            orgB = TestData.Organisation("Org B");
            SeedTeamMember(ctx, orgA, lastName: "MembreOrgA", email: "a@test.be");
            SeedTeamMember(ctx, orgB, lastName: "MembreOrgB", email: "b@test.be");
            ctx.AddRange(orgA, orgB);
            return 0;
        });

        var client = factory.CreateClientFor("u1", orgB.Id, "Coordinator");
        var response = await client.GetAsync("/TeamMembers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("MembreOrgB");
        html.Should().NotContain("MembreOrgA");
    }

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/TeamMembers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------- Details ----------------

    [Fact]
    public async Task Details_ExistingMemberInOwnOrg_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org, firstName: "Sophie", lastName: "Moniteur");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/TeamMembers/Details/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Moniteur");
    }

    [Fact]
    public async Task Details_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/TeamMembers/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_MemberOfAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org);
            ctx.Add(org);
            return 0;
        });

        // Coordinator of a different organisation must not see another org's member.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/TeamMembers/Details/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------- Create (GET) ----------------

    [Fact]
    public async Task CreateForm_AuthenticatedUser_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/TeamMembers/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------- Create (POST) ----------------

    // Create happy path: a member can be created WITHOUT uploading a license file. LicenseUrl is a
    // required (NOT NULL) column; the entity now defaults it to "" so the save succeeds (previously
    // it saved null and 500'd). A valid model redirects and persists with an empty LicenseUrl.
    [Fact]
    public async Task Create_ValidModel_RedirectsAndPersistsWithEmptyLicenseUrl()
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
        var response = await client.PostAsync("/TeamMembers/Create",
            new FormUrlEncodedContent(ValidCreateForm(org.Id)));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        await using var db = factory.NewDbContext();
        var created = await db.TeamMembers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Email == "nouvelle.recrue@test.be");
        created.Should().NotBeNull();
        created!.LicenseUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_MissingRequiredFields_ReturnsOk_AndDoesNotPersist()
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
        var form = ValidCreateForm(org.Id);
        form["FirstName"] = "";   // required
        form["LastName"] = "";    // required
        form["Email"] = "missingfields@test.be";

        var response = await client.PostAsync("/TeamMembers/Create", new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-renders the view with errors

        await using var db = factory.NewDbContext();
        var exists = await db.TeamMembers.IgnoreQueryFilters()
            .AnyAsync(t => t.Email == "missingfields@test.be");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Create_InvalidNationalRegisterNumber_ReturnsOk_AndDoesNotPersist()
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
        var form = ValidCreateForm(org.Id);
        form["NationalRegisterNumber"] = "85.06.15-133.81"; // wrong check digits
        form["Email"] = "badnrn@test.be";

        var response = await client.PostAsync("/TeamMembers/Create", new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = factory.NewDbContext();
        var exists = await db.TeamMembers.IgnoreQueryFilters()
            .AnyAsync(t => t.Email == "badnrn@test.be");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.PostAsync("/TeamMembers/Create",
            new FormUrlEncodedContent(ValidCreateForm(org.Id)));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------- Edit ----------------

    [Fact]
    public async Task EditForm_ExistingMember_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org, lastName: "AModifier");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/TeamMembers/Edit/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("AModifier");
    }

    [Fact]
    public async Task Edit_ValidModel_Redirects_And_Persists()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org);
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = ValidCreateForm(org.Id);
        form["TeamMemberId"] = tm.TeamMemberId.ToString();
        form["FirstName"] = "PrenomMisAJour";
        form["Email"] = "tom.animateur@test.be";
        // No new license file: the existing (seeded) LicenseUrl is preserved on update.
        var response = await client.PostAsync($"/TeamMembers/Edit/{tm.TeamMemberId}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var db = factory.NewDbContext();
        var updated = await db.TeamMembers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TeamMemberId == tm.TeamMemberId);
        updated.Should().NotBeNull();
        updated!.FirstName.Should().Be("PrenomMisAJour");
    }

    [Fact]
    public async Task Edit_RouteIdMismatch_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org);
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = ValidCreateForm(org.Id);
        form["TeamMemberId"] = tm.TeamMemberId.ToString(); // body id differs from route id

        var response = await client.PostAsync($"/TeamMembers/Edit/{tm.TeamMemberId + 1}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_InvalidModel_ReturnsOk_AndDoesNotPersistChange()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org, firstName: "Original");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var form = ValidCreateForm(org.Id);
        form["TeamMemberId"] = tm.TeamMemberId.ToString();
        form["FirstName"] = ""; // required -> invalid

        var response = await client.PostAsync($"/TeamMembers/Edit/{tm.TeamMemberId}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = factory.NewDbContext();
        var unchanged = await db.TeamMembers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TeamMemberId == tm.TeamMemberId);
        unchanged!.FirstName.Should().Be("Original");
    }

    // ---------------- Delete ----------------

    [Fact]
    public async Task DeleteConfirmed_ExistingMember_Redirects_And_Removes()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org);
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync($"/TeamMembers/Delete/{tm.TeamMemberId}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("TeamMembers");

        await using var db = factory.NewDbContext();
        var gone = await db.TeamMembers.IgnoreQueryFilters()
            .AnyAsync(t => t.TeamMemberId == tm.TeamMemberId);
        gone.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteConfirmed_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.PostAsync("/TeamMembers/Delete/999999",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteConfirmed_MemberOfAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org);
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.PostAsync($"/TeamMembers/Delete/{tm.TeamMemberId}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Still present in the database (was not deleted across tenants).
        await using var db = factory.NewDbContext();
        var stillThere = await db.TeamMembers.IgnoreQueryFilters()
            .AnyAsync(t => t.TeamMemberId == tm.TeamMemberId);
        stillThere.Should().BeTrue();
    }
}
