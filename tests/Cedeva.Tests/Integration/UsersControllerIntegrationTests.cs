using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class UsersControllerIntegrationTests
{
    // The UsersController is [Authorize(Roles = "Admin")] and drives Identity's UserManager.
    // Because the test factory disables startup seeding, the "Admin"/"Coordinator" Identity roles
    // do not exist by default. AddToRoleAsync requires the role to exist, so we seed them.
    private static void SeedIdentityRoles(CedevaDbContext ctx)
    {
        ctx.Roles.AddRange(
            new IdentityRole { Name = "Admin", NormalizedName = "ADMIN" },
            new IdentityRole { Name = "Coordinator", NormalizedName = "COORDINATOR" });
    }

    private static CedevaUser BuildUser(
        string firstName, string lastName, string email, int? organisationId, Role role)
    {
        var id = Guid.NewGuid().ToString();
        return new CedevaUser
        {
            Id = id,
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
    // Authorization
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Index_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Index_AsCoordinator_IsForbidden()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", organisationId: 1, role: "Coordinator");
        var response = await client.GetAsync("/Users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----------------------------------------------------------------------------------------
    // GET Index
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Index_AsAdmin_ListsUsers()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            ctx.Add(BuildUser("Alice", "Alpha", "alice.alpha@test.be", org.Id, Role.Coordinator));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("alice.alpha@test.be");
    }

    [Fact]
    public async Task Index_WithSearchString_FiltersResults()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.AddRange(
                BuildUser("Alice", "Alpha", "alice.alpha@test.be", null, Role.Coordinator),
                BuildUser("Bob", "Beta", "bob.beta@test.be", null, Role.Coordinator));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users?SearchString=Alpha");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("alice.alpha@test.be");
        html.Should().NotContain("bob.beta@test.be");
    }

    // ----------------------------------------------------------------------------------------
    // GET Details
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Details_ExistingUser_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var userId = factory.Seed(ctx =>
        {
            var u = BuildUser("Carol", "Charlie", "carol.charlie@test.be", null, Role.Coordinator);
            ctx.Add(u);
            return u.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync($"/Users/Details/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("carol.charlie@test.be");
    }

    [Fact]
    public async Task Details_UnknownUser_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users/Details/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------------------------------------
    // GET Create
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Get_ReturnsForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.Add(TestData.Organisation());
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ----------------------------------------------------------------------------------------
    // POST Create
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Post_Valid_PersistsAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            SeedIdentityRoles(ctx);
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            return org.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "New",
            ["LastName"] = "User",
            ["Email"] = "new.user@test.be",
            ["OrganisationId"] = orgId.ToString(),
            ["Role"] = nameof(Role.Coordinator),
            ["EmailConfirmed"] = "true",
            ["Password"] = "Passw0rd!",
            ["ConfirmPassword"] = "Passw0rd!"
        });

        var response = await client.PostAsync("/Users/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Users/Details");

        using var db = factory.NewDbContext();
        var persisted = await db.Users.FirstOrDefaultAsync(u => u.Email == "new.user@test.be");
        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be("New");
        persisted.OrganisationId.Should().Be(orgId);
    }

    [Fact]
    public async Task Create_Post_MissingPassword_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx => { SeedIdentityRoles(ctx); return 0; });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "No",
            ["LastName"] = "Password",
            ["Email"] = "nopassword@test.be",
            ["Role"] = nameof(Role.Coordinator),
            ["EmailConfirmed"] = "true"
            // Password intentionally omitted -> controller adds ModelState error
        });

        var response = await client.PostAsync("/Users/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK); // view re-rendered

        using var db = factory.NewDbContext();
        (await db.Users.AnyAsync(u => u.Email == "nopassword@test.be")).Should().BeFalse();
    }

    [Fact]
    public async Task Create_Post_MissingRequiredFields_ReturnsViewAndDoesNotPersist()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx => { SeedIdentityRoles(ctx); return 0; });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            // FirstName/LastName missing (both [Required])
            ["Email"] = "missing.fields@test.be",
            ["Role"] = nameof(Role.Coordinator),
            ["Password"] = "Passw0rd!",
            ["ConfirmPassword"] = "Passw0rd!"
        });

        var response = await client.PostAsync("/Users/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        (await db.Users.AnyAsync(u => u.Email == "missing.fields@test.be")).Should().BeFalse();
    }

    [Fact]
    public async Task Create_Post_WeakPassword_ReturnsViewWithIdentityErrors()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx => { SeedIdentityRoles(ctx); return 0; });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Weak",
            ["LastName"] = "Password",
            ["Email"] = "weak.password@test.be",
            ["Role"] = nameof(Role.Coordinator),
            ["EmailConfirmed"] = "true",
            ["Password"] = "abc", // too short, no digit/upper -> Identity rejects
            ["ConfirmPassword"] = "abc"
        });

        var response = await client.PostAsync("/Users/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-rendered with errors

        using var db = factory.NewDbContext();
        (await db.Users.AnyAsync(u => u.Email == "weak.password@test.be")).Should().BeFalse();
    }

    // ----------------------------------------------------------------------------------------
    // GET Edit
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Edit_Get_ExistingUser_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var userId = factory.Seed(ctx =>
        {
            var u = BuildUser("Edit", "Me", "edit.me@test.be", null, Role.Coordinator);
            ctx.Add(u);
            return u.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync($"/Users/Edit/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Get_UnknownUser_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users/Edit/missing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------------------------------------
    // POST Edit
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Edit_Post_Valid_UpdatesAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var userId = factory.Seed(ctx =>
        {
            SeedIdentityRoles(ctx);
            var u = BuildUser("Before", "Name", "before.name@test.be", null, Role.Coordinator);
            ctx.Add(u);
            return u.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = userId,
            ["FirstName"] = "After",
            ["LastName"] = "Name",
            ["Email"] = "after.name@test.be",
            ["Role"] = nameof(Role.Admin),
            ["EmailConfirmed"] = "true"
        });

        var response = await client.PostAsync($"/Users/Edit/{userId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Users/Details");

        using var db = factory.NewDbContext();
        var persisted = await db.Users.FirstAsync(u => u.Id == userId);
        persisted.FirstName.Should().Be("After");
        persisted.Email.Should().Be("after.name@test.be");
        persisted.Role.Should().Be(Role.Admin);
    }

    [Fact]
    public async Task Edit_Post_MismatchedId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var userId = factory.Seed(ctx =>
        {
            var u = BuildUser("Mismatch", "Id", "mismatch.id@test.be", null, Role.Coordinator);
            ctx.Add(u);
            return u.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "a-different-id",
            ["FirstName"] = "X",
            ["LastName"] = "Y",
            ["Email"] = "mismatch.id@test.be",
            ["Role"] = nameof(Role.Coordinator)
        });

        // Route id differs from body Id -> controller returns NotFound.
        var response = await client.PostAsync($"/Users/Edit/{userId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_Post_InvalidModel_ReturnsViewAndDoesNotUpdate()
    {
        using var factory = new CedevaWebApplicationFactory();
        var userId = factory.Seed(ctx =>
        {
            var u = BuildUser("Keep", "Original", "keep.original@test.be", null, Role.Coordinator);
            ctx.Add(u);
            return u.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = userId,
            // FirstName missing -> [Required] fails
            ["LastName"] = "Original",
            ["Email"] = "keep.original@test.be",
            ["Role"] = nameof(Role.Coordinator)
        });

        var response = await client.PostAsync($"/Users/Edit/{userId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK); // view re-rendered

        using var db = factory.NewDbContext();
        var persisted = await db.Users.FirstAsync(u => u.Id == userId);
        persisted.FirstName.Should().Be("Keep"); // unchanged
    }

    // ----------------------------------------------------------------------------------------
    // GET Delete
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Get_ExistingUser_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        var userId = factory.Seed(ctx =>
        {
            var u = BuildUser("Delete", "Confirm", "delete.confirm@test.be", null, Role.Coordinator);
            ctx.Add(u);
            return u.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync($"/Users/Delete/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_Get_UnknownUser_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var response = await client.GetAsync("/Users/Delete/missing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------------------------------------
    // POST Delete (action name "Delete" -> DeleteConfirmed)
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Post_RemovesUserAndRedirectsToIndex()
    {
        using var factory = new CedevaWebApplicationFactory();
        var userId = factory.Seed(ctx =>
        {
            var u = BuildUser("Gone", "Soon", "gone.soon@test.be", null, Role.Coordinator);
            ctx.Add(u);
            return u.Id;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, role: "Admin");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = userId
        });

        var response = await client.PostAsync($"/Users/Delete/{userId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/Users");

        using var db = factory.NewDbContext();
        (await db.Users.AnyAsync(u => u.Id == userId)).Should().BeFalse();
    }
}
