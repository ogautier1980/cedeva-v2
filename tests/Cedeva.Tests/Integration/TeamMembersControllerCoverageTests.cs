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

/// <summary>
/// Additional coverage for <c>TeamMembersController</c> targeting actions and branches NOT exercised
/// by <c>TeamMembersControllerIntegrationTests</c>: Index search/sort/redirect/empty/admin branches,
/// Export (Excel) + ExportPdf, Delete (GET), Create (GET) admin dropdown, Edit (GET) not-found/cross-tenant,
/// Edit (POST) not-found after id match, and the ViewLicense / DeleteLicense endpoints.
/// </summary>
[Collection("WebApp")]
public class TeamMembersControllerCoverageTests
{
    // Valid Belgian national register number (passes the modulo-97 check).
    private const string ValidNrnStored = "85061513380";

    private static TeamMember SeedTeamMember(CedevaDbContext ctx, Organisation org,
        string firstName = "Tom", string lastName = "Animateur",
        string email = "tom.animateur@test.be",
        TeamRole role = TeamRole.Animator, Status status = Status.Volunteer,
        string? licenseUrl = "/uploads/seed/license.pdf")
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
            TeamRole = role,
            License = License.License,
            Status = status,
            LicenseUrl = licenseUrl!,
            Organisation = org
        };

        ctx.AddRange(address, teamMember);
        return teamMember;
    }

    // ---------------- Index: search / sort / redirect / empty / admin ----------------

    [Fact]
    public async Task Index_WithQueryParams_RedirectsToCleanUrl()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            SeedTeamMember(ctx, org);
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/TeamMembers?searchString=Tom&sortBy=lastname&sortOrder=desc");

        // hasQueryParams branch => 302 redirect to the clean Index URL ("/TeamMembers", not "Index").
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("TeamMembers");
        response.Headers.Location!.ToString().Should().NotContain("Index");
    }

    [Fact]
    public async Task Index_SearchString_FiltersResults()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            SeedTeamMember(ctx, org, firstName: "Alice", lastName: "Trouvable", email: "alice@test.be");
            SeedTeamMember(ctx, org, firstName: "Bob", lastName: "Cachemoi", email: "bob@test.be");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        // First request stores the filter in session and redirects.
        var redirect = await client.GetAsync("/TeamMembers?searchString=Trouvable");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        // Follow the redirect manually; the session filter is applied on the clean URL.
        var listed = await client.GetAsync(redirect.Headers.Location!.ToString());
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await listed.Content.ReadAsStringAsync();
        html.Should().Contain("Trouvable");
        html.Should().NotContain("Cachemoi");
    }

    [Fact]
    public async Task Index_SearchWithNoMatches_RendersNoMembersFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            SeedTeamMember(ctx, org, lastName: "Existant");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var redirect = await client.GetAsync("/TeamMembers?searchString=ZZZ_NoSuchPerson");
        var listed = await client.GetAsync(redirect.Headers.Location!.ToString());

        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await listed.Content.ReadAsStringAsync();
        html.Should().NotContain("Existant");
        // Empty-with-search branch of the view (not the "no members yet" branch).
        html.Should().Contain("text-center");
    }

    [Fact]
    public async Task Index_AdminSeesMembersOfAllOrganisations()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var orgA = TestData.Organisation("Org A");
            var orgB = TestData.Organisation("Org B");
            SeedTeamMember(ctx, orgA, lastName: "MembreA", email: "a@test.be");
            SeedTeamMember(ctx, orgB, lastName: "MembreB", email: "b@test.be");
            ctx.AddRange(orgA, orgB);
            return 0;
        });

        // Admin bypasses the tenancy query filter (no OrganisationId required).
        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.GetAsync("/TeamMembers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("MembreA");
        html.Should().Contain("MembreB");
    }

    [Fact]
    public async Task Index_EmptyOrganisation_RendersNoMembersYet()
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
        var response = await client.GetAsync("/TeamMembers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // No-search empty branch surfaces the Create call-to-action button.
        html.Should().Contain("Create");
    }

    // ---------------- Export (Excel) ----------------

    [Fact]
    public async Task Export_ReturnsXlsx_WithContent()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            SeedTeamMember(ctx, org, lastName: "Exportable");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/TeamMembers/Export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_WithSearchString_FiltersAndReturnsXlsx()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            SeedTeamMember(ctx, org, lastName: "Garde", email: "garde@test.be");
            SeedTeamMember(ctx, org, lastName: "Autre", email: "autre@test.be");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/TeamMembers/Export?searchString=Garde");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    // ---------------- ExportPdf ----------------

    [Fact]
    public async Task ExportPdf_ReturnsPdf_WithContent()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            SeedTeamMember(ctx, org, lastName: "PdfMember");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/TeamMembers/ExportPdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/TeamMembers/Export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------- Delete (GET) confirmation page ----------------

    [Fact]
    public async Task DeleteForm_ExistingMember_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org, lastName: "ASupprimer");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/TeamMembers/Delete/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("ASupprimer");
    }

    [Fact]
    public async Task DeleteForm_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/TeamMembers/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteForm_MemberOfAnotherOrganisation_ReturnsNotFound()
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
        var response = await client.GetAsync($"/TeamMembers/Delete/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------- Create (GET) admin dropdown branch ----------------

    [Fact]
    public async Task CreateForm_Admin_ReturnsOk_WithOrganisationsDropdown()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation("Org Alpha"));
            ctx.Add(TestData.Organisation("Org Beta"));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.GetAsync("/TeamMembers/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // Admin path populates ViewBag.Organisations -> options rendered for selection.
        html.Should().Contain("Org Alpha");
        html.Should().Contain("Org Beta");
    }

    // ---------------- Edit (GET) not-found / cross-tenant ----------------

    [Fact]
    public async Task EditForm_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/TeamMembers/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditForm_MemberOfAnotherOrganisation_ReturnsNotFound()
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
        var response = await client.GetAsync($"/TeamMembers/Edit/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------- Edit (POST) not-found after id-match (member missing) ----------------

    [Fact]
    public async Task Edit_ValidModel_MemberDoesNotExist_ReturnsNotFound()
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

        const int missingId = 424242;
        var form = new Dictionary<string, string>
        {
            ["TeamMemberId"] = missingId.ToString(), // matches the route id, so id != viewModel.TeamMemberId is false
            ["FirstName"] = "Fantome",
            ["LastName"] = "Inexistant",
            ["Email"] = "ghost@test.be",
            ["MobilePhoneNumber"] = "0470123456",
            ["NationalRegisterNumber"] = "85.06.15-133.80",
            ["BirthDate"] = "1985-06-15",
            ["Street"] = "Rue Neuve",
            ["City"] = "Liège",
            ["PostalCode"] = "4000",
            ["Country"] = Country.Belgium.ToString(),
            ["TeamRole"] = TeamRole.Animator.ToString(),
            ["License"] = License.License.ToString(),
            ["Status"] = Status.Volunteer.ToString(),
            ["OrganisationId"] = org.Id.ToString()
        };

        var response = await client.PostAsync($"/TeamMembers/Edit/{missingId}",
            new FormUrlEncodedContent(form));

        // Model is valid and id matches, but GetByIdAsync returns null -> NotFound.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_ValidModel_WithReturnUrl_RedirectsToReturnUrl()
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
        var form = new Dictionary<string, string>
        {
            ["TeamMemberId"] = tm.TeamMemberId.ToString(),
            ["FirstName"] = "AvecRetour",
            ["LastName"] = "Animateur",
            ["Email"] = "tom.animateur@test.be",
            ["MobilePhoneNumber"] = "0470123456",
            ["NationalRegisterNumber"] = "85.06.15-133.80",
            ["BirthDate"] = "1985-06-15",
            ["Street"] = "Rue Neuve",
            ["City"] = "Liège",
            ["PostalCode"] = "4000",
            ["Country"] = Country.Belgium.ToString(),
            ["TeamRole"] = TeamRole.Animator.ToString(),
            ["License"] = License.License.ToString(),
            ["Status"] = Status.Volunteer.ToString(),
            ["OrganisationId"] = org.Id.ToString()
        };

        var response = await client.PostAsync(
            $"/TeamMembers/Edit/{tm.TeamMemberId}?returnUrl=%2FTeamMembers%3FsearchString%3DAvecRetour",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        // RedirectToReturnUrlOrAction honours the (local) returnUrl rather than Details.
        response.Headers.Location!.ToString().Should().Contain("/TeamMembers");
        response.Headers.Location!.ToString().Should().Contain("searchString");

        await using var db = factory.NewDbContext();
        var updated = await db.TeamMembers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TeamMemberId == tm.TeamMemberId);
        updated!.FirstName.Should().Be("AvecRetour");
    }

    // ---------------- ViewLicense ----------------

    [Fact]
    public async Task ViewLicense_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/TeamMembers/ViewLicense/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ViewLicense_MemberOfAnotherOrganisation_ReturnsForbidden()
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

        // ViewLicense bypasses the query filter then does an explicit tenancy check => Forbid (403).
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/TeamMembers/ViewLicense/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ViewLicense_NoLicenseUrl_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            // LicenseUrl column is NOT NULL; use empty string to hit the IsNullOrEmpty branch.
            tm = SeedTeamMember(ctx, org, licenseUrl: "");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/TeamMembers/ViewLicense/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ViewLicense_LocalFileMissing_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            // Seeded LicenseUrl starts with /uploads/ but no physical file exists.
            tm = SeedTeamMember(ctx, org, licenseUrl: "/uploads/missing/no-such-file.pdf");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/TeamMembers/ViewLicense/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ViewLicense_Admin_AzureBlobUrl_Redirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            // Non-/uploads/ URL => treated as an Azure Blob URL and redirected.
            tm = SeedTeamMember(ctx, org, licenseUrl: "https://blob.example.com/licenses/abc.pdf");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.GetAsync($"/TeamMembers/ViewLicense/{tm.TeamMemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("https://blob.example.com/licenses/abc.pdf");
    }

    // ---------------- DeleteLicense ----------------

    // DeleteLicense clears the license by storing an empty string (the LicenseUrl column is
    // IsRequired/NOT NULL, so null cannot be persisted). The request succeeds and the value is empty.
    [Fact]
    public async Task DeleteLicense_ExistingMember_ClearsLicenseUrl()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        TeamMember tm = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            tm = SeedTeamMember(ctx, org, licenseUrl: "/uploads/missing/no-such-file.pdf");
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync($"/TeamMembers/DeleteLicense/{tm.TeamMemberId}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = factory.NewDbContext();
        var updated = await db.TeamMembers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TeamMemberId == tm.TeamMemberId);
        updated!.LicenseUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteLicense_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.PostAsync("/TeamMembers/DeleteLicense/999999",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteLicense_MemberOfAnotherOrganisation_ReturnsForbidden()
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
        var response = await client.PostAsync($"/TeamMembers/DeleteLicense/{tm.TeamMemberId}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Cross-tenant member's LicenseUrl must be untouched.
        await using var db = factory.NewDbContext();
        var unchanged = await db.TeamMembers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TeamMemberId == tm.TeamMemberId);
        unchanged!.LicenseUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ViewLicense_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/TeamMembers/ViewLicense/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
