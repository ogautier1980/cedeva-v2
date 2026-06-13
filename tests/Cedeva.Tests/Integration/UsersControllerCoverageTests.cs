using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Additional coverage for <c>UsersController</c> branches not exercised by
/// <c>UsersControllerIntegrationTests</c>: organisation filter, all sort permutations,
/// pagination, empty list, Admin-role creation path, password-change path on edit,
/// edit of unknown user with a valid model, delete-confirmed redirect/persistence,
/// Excel/PDF export (content + filters), and authorization on every action.
/// </summary>
[Collection("WebApp")]
public class UsersControllerCoverageTests
{
    private static void SeedIdentityRoles(CedevaDbContext ctx)
    {
        ctx.Roles.AddRange(
            new IdentityRole { Name = "Admin", NormalizedName = "ADMIN" },
            new IdentityRole { Name = "Coordinator", NormalizedName = "COORDINATOR" });
    }

    private static CedevaUser BuildUser(
        string firstName, string lastName, string email, int? organisationId, Role role)
    {
        return new CedevaUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            OrganisationId = organisationId,
            Role = role,
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedBy = "seed"
        };
    }

    // ----------------------------------------------------------------------------------------
    // GET Index — organisation filter
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Index_WithOrganisationFilter_OnlyShowsThatOrganisation()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgAId = factory.Seed(ctx =>
        {
            var orgA = TestData.Organisation("Org A");
            var orgB = TestData.Organisation("Org B");
            ctx.AddRange(orgA, orgB);
            ctx.SaveChanges();
            ctx.Add(BuildUser("InOrgA", "User", "inorga.user@test.be", orgA.Id, Role.Coordinator));
            ctx.Add(BuildUser("InOrgB", "User", "inorgb.user@test.be", orgB.Id, Role.Coordinator));
            return orgA.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync($"/Users?OrganisationId={orgAId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("inorga.user@test.be");
        html.Should().NotContain("inorgb.user@test.be");
    }

    // ----------------------------------------------------------------------------------------
    // GET Index — sorting permutations (each switch arm)
    // ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("firstname", "asc")]
    [InlineData("firstname", "desc")]
    [InlineData("lastname", "desc")]
    [InlineData("email", "asc")]
    [InlineData("email", "desc")]
    [InlineData("role", "asc")]
    [InlineData("role", "desc")]
    [InlineData("organisationname", "asc")]
    [InlineData("organisationname", "desc")]
    [InlineData("unknown", "asc")] // default branch
    public async Task Index_WithSorting_ReturnsOkAndAllUsers(string sortBy, string sortOrder)
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation("Sort Org");
            ctx.Add(org);
            ctx.SaveChanges();
            ctx.AddRange(
                BuildUser("Zoe", "Zulu", "zoe.zulu@test.be", org.Id, Role.Admin),
                BuildUser("Adam", "Able", "adam.able@test.be", org.Id, Role.Coordinator));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync($"/Users?SortBy={sortBy}&SortOrder={sortOrder}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("zoe.zulu@test.be");
        html.Should().Contain("adam.able@test.be");
    }

    // ----------------------------------------------------------------------------------------
    // GET Index — pagination (page 2)
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Index_WithPagination_HonorsPageSizeAndPageNumber()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            for (var i = 0; i < 5; i++)
            {
                // LastName drives the default sort; pad index so order is deterministic.
                ctx.Add(BuildUser($"User{i}", $"Last{i:00}", $"user{i}@test.be", null, Role.Coordinator));
            }
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");

        var page1 = await client.GetAsync("/Users?PageNumber=1&PageSize=2");
        var page2 = await client.GetAsync("/Users?PageNumber=2&PageSize=2");

        page1.StatusCode.Should().Be(HttpStatusCode.OK);
        page2.StatusCode.Should().Be(HttpStatusCode.OK);

        var html1 = await page1.Content.ReadAsStringAsync();
        var html2 = await page2.Content.ReadAsStringAsync();

        // First two (by LastName) on page 1, next two on page 2; no overlap of the first user.
        html1.Should().Contain("user0@test.be");
        html2.Should().NotContain("user0@test.be");
        html2.Should().Contain("user2@test.be");
    }

    // ----------------------------------------------------------------------------------------
    // GET Index — empty result set
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Index_NoMatchingUsers_ReturnsOkWithoutRows()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(BuildUser("Only", "User", "only.user@test.be", null, Role.Coordinator));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users?SearchString=zzz-no-match");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("only.user@test.be");
    }

    // ----------------------------------------------------------------------------------------
    // POST Create — Admin role path (RoleAdmin assignment branch)
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Post_AsAdminRole_PersistsWithAdminRoleAssigned()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx => { SeedIdentityRoles(ctx); return 0; });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Admin",
            ["LastName"] = "Created",
            ["Email"] = "admin.created@test.be",
            ["Role"] = nameof(Role.Admin),
            ["EmailConfirmed"] = "true",
            ["Password"] = "Passw0rd!",
            ["ConfirmPassword"] = "Passw0rd!"
        });

        var response = await client.PostAsync("/Users/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Users/Details");

        using var db = factory.NewDbContext();
        var persisted = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin.created@test.be");
        persisted.Should().NotBeNull();
        persisted!.Role.Should().Be(Role.Admin);

        var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");
        var hasAdminRole = await db.UserRoles
            .AnyAsync(ur => ur.UserId == persisted.Id && ur.RoleId == adminRole.Id);
        hasAdminRole.Should().BeTrue();
    }

    [Fact]
    public async Task Create_Post_DuplicateEmail_ReturnsViewWithIdentityErrors()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            SeedIdentityRoles(ctx);
            ctx.Add(BuildUser("Existing", "User", "dup@test.be", null, Role.Coordinator));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Another",
            ["LastName"] = "User",
            ["Email"] = "dup@test.be", // already taken
            ["Role"] = nameof(Role.Coordinator),
            ["EmailConfirmed"] = "true",
            ["Password"] = "Passw0rd!",
            ["ConfirmPassword"] = "Passw0rd!"
        });

        var response = await client.PostAsync("/Users/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-rendered with Identity errors

        using var db = factory.NewDbContext();
        (await db.Users.CountAsync(u => u.Email == "dup@test.be")).Should().Be(1);
    }

    // ----------------------------------------------------------------------------------------
    // POST Edit — password-change path + role change (Admin -> Coordinator)
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Edit_Post_WithNewPassword_UpdatesPasswordHashAndRole()
    {
        using var factory = new CedevaWebApplicationFactory();
        string originalHash;
        var userId = factory.Seed(ctx =>
        {
            SeedIdentityRoles(ctx);
            var u = BuildUser("Pwd", "Change", "pwd.change@test.be", null, Role.Admin);
            u.PasswordHash = "ORIGINAL-HASH-PLACEHOLDER";
            ctx.Add(u);
            return u.Id;
        });

        using (var preDb = factory.NewDbContext())
        {
            originalHash = (await preDb.Users.FirstAsync(u => u.Id == userId)).PasswordHash!;
        }

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = userId,
            ["FirstName"] = "Pwd",
            ["LastName"] = "Change",
            ["Email"] = "pwd.change@test.be",
            ["Role"] = nameof(Role.Coordinator), // role downgrade exercises UpdateUserRole
            ["EmailConfirmed"] = "true",
            ["Password"] = "BrandNew1!",
            ["ConfirmPassword"] = "BrandNew1!"
        });

        var response = await client.PostAsync($"/Users/Edit/{userId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Users/Details");

        using var db = factory.NewDbContext();
        var persisted = await db.Users.FirstAsync(u => u.Id == userId);
        persisted.Role.Should().Be(Role.Coordinator);
        persisted.PasswordHash.Should().NotBe(originalHash);
        persisted.PasswordHash.Should().NotBeNullOrEmpty();

        var coordRole = await db.Roles.FirstAsync(r => r.Name == "Coordinator");
        (await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == coordRole.Id))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Edit_Post_ValidModelButUnknownUser_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx => { SeedIdentityRoles(ctx); return 0; });

        const string ghostId = "ghost-user-id";
        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            // Route id == body Id (passes mismatch check) but user does not exist.
            ["Id"] = ghostId,
            ["FirstName"] = "Ghost",
            ["LastName"] = "User",
            ["Email"] = "ghost.user@test.be",
            ["Role"] = nameof(Role.Coordinator),
            ["EmailConfirmed"] = "true"
        });

        var response = await client.PostAsync($"/Users/Edit/{ghostId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------------------------------------
    // POST Delete — redirect target is the Index ("/Users", not containing "Index")
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Post_UnknownUser_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "no-such-user"
        });

        var response = await client.PostAsync("/Users/Delete/no-such-user", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------------------------------------
    // GET Export (Excel)
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Export_AsAdmin_ReturnsNonEmptyExcel()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation("Export Org");
            ctx.Add(org);
            ctx.SaveChanges();
            ctx.Add(BuildUser("Export", "Me", "export.me@test.be", org.Id, Role.Coordinator));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users/Export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Export_WithSearchAndOrganisationFilter_ReturnsNonEmptyExcel()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation("Filter Export Org");
            ctx.Add(org);
            ctx.SaveChanges();
            ctx.Add(BuildUser("Filtered", "Export", "filtered.export@test.be", org.Id, Role.Coordinator));
            return org.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync($"/Users/Export?searchString=Filtered&organisationId={orgId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    // ----------------------------------------------------------------------------------------
    // GET ExportPdf
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task ExportPdf_AsAdmin_ReturnsNonEmptyPdf()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation("Pdf Org");
            ctx.Add(org);
            ctx.SaveChanges();
            ctx.Add(BuildUser("Pdf", "Me", "pdf.me@test.be", org.Id, Role.Admin));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users/ExportPdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportPdf_WithSearchFilter_ReturnsNonEmptyPdf()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(BuildUser("Searchable", "Pdf", "searchable.pdf@test.be", null, Role.Coordinator));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users/ExportPdf?searchString=Searchable");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    // ----------------------------------------------------------------------------------------
    // Authorization across actions
    // ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("/Users/Create")]
    [InlineData("/Users/Export")]
    [InlineData("/Users/ExportPdf")]
    public async Task GetActions_AsCoordinator_AreForbidden(string url)
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("coord", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/Users/Create")]
    [InlineData("/Users/Export")]
    [InlineData("/Users/ExportPdf")]
    public async Task GetActions_Unauthenticated_AreChallenged(string url)
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Post_AsCoordinator_IsForbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx => { SeedIdentityRoles(ctx); return 0; });

        var client = factory.CreateClientFor("coord", organisationId: 1, role: "Coordinator");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Should",
            ["LastName"] = "Fail",
            ["Email"] = "should.fail@test.be",
            ["Role"] = nameof(Role.Coordinator),
            ["Password"] = "Passw0rd!",
            ["ConfirmPassword"] = "Passw0rd!"
        });

        var response = await client.PostAsync("/Users/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var db = factory.NewDbContext();
        (await db.Users.AnyAsync(u => u.Email == "should.fail@test.be")).Should().BeFalse();
    }
}
