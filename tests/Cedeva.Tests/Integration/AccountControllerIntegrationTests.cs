using System.Net;
using System.Net.Http;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="Cedeva.Website.Features.Account.AccountController"/>.
///
/// Identity users are NOT seeded by the test factory (the background startup seeder is disabled),
/// so a real "login succeeds" path cannot be exercised deterministically. These tests therefore
/// cover: anonymous GET pages render (200), invalid POSTs re-render the view with validation
/// errors (200), failed logins for unknown users stay on the page (200), and the authenticated-only
/// endpoints (Logout/Profile) reject anonymous callers.
/// </summary>
[Collection("WebApp")]
public class AccountControllerIntegrationTests
{
    private static HttpClient AnonymousClient(CedevaWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // ---------------------------------------------------------------- GET pages (anonymous)

    [Fact]
    public async Task Login_Get_Anonymous_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = AnonymousClient(factory);

        var response = await client.GetAsync("/Account/Login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_Get_Anonymous_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.AddRange(TestData.Organisation("Org Alpha"));
            return 0;
        });
        var client = AnonymousClient(factory);

        var response = await client.GetAsync("/Account/Register");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Org Alpha"); // organisation dropdown is populated
    }

    [Fact]
    public async Task AccessDenied_Get_Anonymous_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = AnonymousClient(factory);

        var response = await client.GetAsync("/Account/AccessDenied");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- Login POST

    [Fact]
    public async Task Login_Post_MissingFields_ReturnsViewWithValidationErrors()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = AnonymousClient(factory);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            // Email and Password both omitted -> ModelState invalid -> View(model)
            ["RememberMe"] = "false"
        });

        var response = await client.PostAsync("/Account/Login", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_Post_MalformedEmail_ReturnsViewWithValidationErrors()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = AnonymousClient(factory);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "not-an-email",
            ["Password"] = "whatever"
        });

        var response = await client.PostAsync("/Account/Login", content);

        // EmailAddress attribute fails -> ModelState invalid -> 200 re-render (no sign-in attempted).
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_Post_UnknownUserWithWrongPassword_StaysOnPage()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = AnonymousClient(factory);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "nobody@cedeva.be",
            ["Password"] = "WrongPassword1"
        });

        var response = await client.PostAsync("/Account/Login", content);

        // PasswordSignInAsync fails (no such user) -> InvalidCredentials -> View(model) 200.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- Register POST

    [Fact]
    public async Task Register_Post_MissingFields_ReturnsViewAndDropdownRepopulated()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.AddRange(TestData.Organisation("Org Beta"));
            return 0;
        });
        var client = AnonymousClient(factory);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            // All required fields omitted -> ModelState invalid -> dropdown repopulated + View(model)
            ["FirstName"] = ""
        });

        var response = await client.PostAsync("/Account/Register", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Org Beta"); // PopulateOrganisationDropdown re-ran on invalid POST
    }

    [Fact]
    public async Task Register_Post_PasswordMismatch_ReturnsView()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation("Org Gamma");
            ctx.Add(org);
            ctx.SaveChanges();
            return org.Id;
        });
        var client = AnonymousClient(factory);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Jean",
            ["LastName"] = "Dupont",
            ["Email"] = "jean.dupont@cedeva.be",
            ["Password"] = "Valid@123456",
            ["ConfirmPassword"] = "Different@123456", // [Compare] fails
            ["OrganisationId"] = orgId.ToString()
        });

        var response = await client.PostAsync("/Account/Register", content);

        // Compare validation fails -> ModelState invalid -> 200, no user created.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Users.Should().NotContain(u => u.Email == "jean.dupont@cedeva.be");
    }

    [Fact]
    public async Task Register_Post_ShortPassword_ReturnsView()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation("Org Delta");
            ctx.Add(org);
            ctx.SaveChanges();
            return org.Id;
        });
        var client = AnonymousClient(factory);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Marie",
            ["LastName"] = "Martin",
            ["Email"] = "marie.martin@cedeva.be",
            ["Password"] = "ab1",       // below MinimumLength = 6 on the view model
            ["ConfirmPassword"] = "ab1",
            ["OrganisationId"] = orgId.ToString()
        });

        var response = await client.PostAsync("/Account/Register", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Users.Should().NotContain(u => u.Email == "marie.martin@cedeva.be");
    }

    [Fact]
    public async Task Register_Post_ValidData_CreatesUserAndRedirects()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            // The controller assigns the "Coordinator" role on success; that role must exist
            // in the store (Identity roles are not seeded by the test factory) or AddToRoleAsync throws.
            ctx.Roles.Add(new IdentityRole { Name = "Coordinator", NormalizedName = "COORDINATOR" });
            var org = TestData.Organisation("Org Epsilon");
            ctx.Add(org);
            ctx.SaveChanges();
            return org.Id;
        });
        var client = AnonymousClient(factory);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Alice",
            ["LastName"] = "Durand",
            ["Email"] = "alice.durand@cedeva.be",
            ["Password"] = "Valid@123456", // satisfies Identity policy (8+, upper, lower, digit)
            ["ConfirmPassword"] = "Valid@123456",
            ["OrganisationId"] = orgId.ToString()
        });

        var response = await client.PostAsync("/Account/Register", content);

        // Successful CreateAsync -> RedirectToLocal -> 302 (anonymous principal => Home).
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        db.Users.Should().Contain(u => u.Email == "alice.durand@cedeva.be");
    }

    [Fact]
    public async Task Register_Post_DuplicateEmail_SecondAttemptDoesNotCreateSecondUser()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            ctx.Roles.Add(new IdentityRole { Name = "Coordinator", NormalizedName = "COORDINATOR" });
            var org = TestData.Organisation("Org Zeta");
            ctx.Add(org);
            ctx.SaveChanges();
            return org.Id;
        });
        var client = AnonymousClient(factory);

        Dictionary<string, string> Form() => new()
        {
            ["FirstName"] = "Bob",
            ["LastName"] = "Leroy",
            ["Email"] = "bob.leroy@cedeva.be",
            ["Password"] = "Valid@123456",
            ["ConfirmPassword"] = "Valid@123456",
            ["OrganisationId"] = orgId.ToString()
        };

        var first = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(Form()));
        first.StatusCode.Should().Be(HttpStatusCode.Found); // first registration succeeds

        var second = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(Form()));
        // CreateAsync fails (duplicate user/email) -> errors added to ModelState -> View(model) 200.
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.NewDbContext();
        db.Users.Where(u => u.Email == "bob.leroy@cedeva.be").Should().HaveCount(1);
    }

    // ---------------------------------------------------------------- Authenticated-only endpoints

    [Fact]
    public async Task Logout_Post_Anonymous_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = AnonymousClient(factory);

        var response = await client.PostAsync("/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        // [Authorize] on Logout: an unauthenticated POST is challenged. The configured
        // application cookie LogoutPath produces a 302 redirect to the login page rather than 401,
        // so the sign-out action never executes for an anonymous caller.
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("Login");
    }

    [Fact]
    public async Task Profile_Get_Anonymous_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = AnonymousClient(factory);

        var response = await client.GetAsync("/Account/Profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Profile_Get_AuthenticatedButNoBackingIdentityUser_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        // Authenticated via TestAuthHandler, but no matching CedevaUser exists in the store,
        // so _userManager.GetUserAsync(User) returns null -> NotFound().
        var client = factory.CreateClientFor("ghost-user", organisationId: 1, role: "Coordinator");

        var response = await client.GetAsync("/Account/Profile");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- Interface language

    private const string LangPassword = "Langue@123456";

    /// <summary>Seeds a real Identity user (Id "lang-user") able to sign in with <see cref="LangPassword"/>.</summary>
    private static int SeedLanguageUser(CedevaWebApplicationFactory factory, string? preferredLanguage)
    {
        return factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            ctx.SaveChanges();
            var user = new CedevaUser
            {
                Id = "lang-user",
                UserName = "lang@cedeva.be",
                NormalizedUserName = "LANG@CEDEVA.BE",
                Email = "lang@cedeva.be",
                NormalizedEmail = "LANG@CEDEVA.BE",
                EmailConfirmed = true,
                FirstName = "Lena",
                LastName = "Taal",
                OrganisationId = org.Id,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedBy = "seed",
                PreferredLanguage = preferredLanguage
            };
            user.PasswordHash = new PasswordHasher<CedevaUser>().HashPassword(user, LangPassword);
            ctx.Add(user);
            return org.Id;
        });
    }

    [Fact]
    public async Task Profile_Post_SavesLanguageAndSwitchesTheCultureCookie()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedLanguageUser(factory, preferredLanguage: null);
        var client = factory.CreateClientFor("lang-user", orgId, "Coordinator");

        var response = await client.PostAsync("/Account/Profile", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Lena",
            ["LastName"] = "Taal",
            ["PreferredLanguage"] = "nl"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        var setCookie = Uri.UnescapeDataString(string.Join(";", response.Headers.GetValues("Set-Cookie")));
        setCookie.Should().Contain(".AspNetCore.Culture=c=nl-BE|uic=nl");
        using var db = factory.NewDbContext();
        db.Users.Single(u => u.Id == "lang-user").PreferredLanguage.Should().Be("nl");
    }

    [Fact]
    public async Task Profile_Post_UnsupportedLanguage_IsRejected()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedLanguageUser(factory, preferredLanguage: "fr");
        var client = factory.CreateClientFor("lang-user", orgId, "Coordinator");

        var response = await client.PostAsync("/Account/Profile", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Lena",
            ["LastName"] = "Taal",
            ["PreferredLanguage"] = "de"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var db = factory.NewDbContext();
        db.Users.Single(u => u.Id == "lang-user").PreferredLanguage.Should().Be("fr");
    }

    [Fact]
    public async Task Profile_Get_PreselectsTheSavedLanguage()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedLanguageUser(factory, preferredLanguage: "en");
        var client = factory.CreateClientFor("lang-user", orgId, "Coordinator");

        var html = await client.GetStringAsync("/Account/Profile");

        html.Should().Contain("<option value=\"en\" selected=\"selected\">English</option>");
    }

    [Fact]
    public async Task Pages_WithTheCultureCookie_AreDisplayedInThatLanguage()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedLanguageUser(factory, preferredLanguage: "nl");
        var client = factory.CreateClientFor("lang-user", orgId, "Coordinator");
        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=" + Uri.EscapeDataString("c=nl-BE|uic=nl"));

        var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Profile"));

        html.Should().Contain("Taal van de interface").And.NotContain("Langue de l'interface");
    }

    [Fact]
    public async Task Login_Post_ReappliesTheSavedLanguage()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedLanguageUser(factory, preferredLanguage: "nl");
        var client = AnonymousClient(factory);

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "lang@cedeva.be",
            ["Password"] = LangPassword,
            ["RememberMe"] = "false"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var setCookie = Uri.UnescapeDataString(string.Join(";", response.Headers.GetValues("Set-Cookie")));
        setCookie.Should().Contain(".AspNetCore.Culture=c=nl-BE|uic=nl");
    }
}
