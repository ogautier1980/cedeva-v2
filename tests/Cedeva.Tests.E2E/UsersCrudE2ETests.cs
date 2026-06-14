using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser CRUD coverage for the admin Users module (ASP.NET Identity-backed). Exercises the real
/// create form (which carries a password field, so the whole flow IS reachable in-test), edit and
/// delete, asserting persisted state through a fresh DbContext. Each test uses a unique email so the
/// shared sequential E2E collection cannot interfere.
///
/// The Role / Organisation dropdowns are enhanced by Choices.js; we drive the visible Choices
/// widget (see <see cref="SelectChoicesAsync"/>) so the posted value matches the user selection.
/// </summary>
[Collection("E2E")]
public class UsersCrudE2ETests
{
    private readonly PlaywrightFixture _fx;

    // The page chrome (nav search, sortable column headers) contains other type=submit buttons, so
    // scope the click to the form rendered inside the page's main card body.
    private const string SubmitButton = ".card-body form button[type=submit]:not(.btn-link):not(.dropdown-item)";

    public UsersCrudE2ETests(PlaywrightFixture fx) => _fx = fx;

    private static string UniqueEmail() => $"e2e.user.{Guid.NewGuid():N}@cedeva-test.be";

    /// <summary>
    /// Selects an option by value on a Choices.js-enhanced &lt;select&gt;. Choices hides the native
    /// &lt;select&gt; and ignores a programmatic value set, so we drive its visible widget: open the
    /// control then click the option whose data-value matches. The widget wraps the original
    /// &lt;select id=...&gt;, so we scope through the enclosing .choices container.
    /// </summary>
    private static async Task SelectChoicesAsync(IPage page, string selectId, string value)
    {
        var widget = page.Locator($".choices:has(#{selectId})");
        await widget.ClickAsync(); // open the dropdown
        // Target the dropdown choice (role=option), not the already-selected chip with same value.
        await widget.Locator($".choices__list--dropdown .choices__item[data-value=\"{value}\"]").ClickAsync();
    }

    private CedevaUser? FindUserByEmail(string email)
    {
        using var db = _fx.Factory.NewDbContext();
        return db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Email == email);
    }

    /// <summary>
    /// The controller calls UserManager.AddToRoleAsync on Create/Edit, which fails if the Identity
    /// role row is absent. The test harness seed creates no roles, so ensure they exist here
    /// (idempotent, shared DB) — this is prerequisite data, not a factory change.
    /// </summary>
    private void EnsureIdentityRoles()
    {
        _fx.Factory.Seed(db =>
        {
            foreach (var name in new[] { "Admin", "Coordinator" })
            {
                var normalized = name.ToUpperInvariant();
                if (!db.Roles.Any(r => r.NormalizedName == normalized))
                {
                    db.Roles.Add(new IdentityRole
                    {
                        Name = name,
                        NormalizedName = normalized
                    });
                }
            }
            return 0;
        });
    }

    [Fact]
    public async Task Create_RendersForm_ReturnsOk()
    {
        await using var ctx = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Users/Create");

        response!.Status.Should().Be(200);
        (await page.Locator("#Email").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#Password").IsVisibleAsync()).Should().BeTrue("creation requires a password");
    }

    [Fact]
    public async Task Create_WithValidData_PersistsUser()
    {
        EnsureIdentityRoles();
        await using var ctx = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/Users/Create");

        var email = UniqueEmail();
        var lastName = $"Zelda{Guid.NewGuid():N}"; // unique marker, no overlap with nav menu words
        await page.FillAsync("#FirstName", "Olivia");
        await page.FillAsync("#LastName", lastName);
        await page.FillAsync("#Email", email);
        // Wait for Choices.js to enhance the selects before driving them.
        await page.Locator(".choices").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await SelectChoicesAsync(page, "Role", Role.Coordinator.ToString());
        await SelectChoicesAsync(page, "OrganisationId", _fx.OrganisationId.ToString());
        await page.FillAsync("#Password", "Test@123456");
        await page.FillAsync("#ConfirmPassword", "Test@123456");

        await page.ClickAsync(SubmitButton);
        await page.WaitForURLAsync("**/Users/Details/**");

        // Lands on Details and shows the unique surname we created.
        (await page.InnerTextAsync("body")).Should().Contain(lastName);

        var persisted = FindUserByEmail(email);
        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be("Olivia");
        persisted.LastName.Should().Be(lastName);
        persisted.Role.Should().Be(Role.Coordinator);
        persisted.OrganisationId.Should().Be(_fx.OrganisationId);
    }

    [Fact]
    public async Task Create_WithMissingPassword_ShowsValidationError_AndDoesNotPersist()
    {
        await using var ctx = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/Users/Create");

        var email = UniqueEmail();
        await page.FillAsync("#FirstName", "NoPass");
        await page.FillAsync("#LastName", $"User{Guid.NewGuid():N}");
        await page.FillAsync("#Email", email);
        // Role defaults to Coordinator; deliberately leave Password / ConfirmPassword empty.

        await page.ClickAsync(SubmitButton);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The controller adds a model error and re-renders the Create view (stays on /Users/Create).
        page.Url.Should().Contain("/Users/Create");
        var body = await page.InnerTextAsync("body");
        body.Should().Contain("mot de passe", "the missing-password model error should be shown");

        FindUserByEmail(email).Should().BeNull("a user without a password must not be created");
    }

    [Fact]
    public async Task Edit_ChangesFields_Persists()
    {
        EnsureIdentityRoles();
        var email = UniqueEmail();
        var userId = _fx.Factory.Seed(db =>
        {
            var u = new CedevaUser
            {
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Before",
                LastName = $"Edit{Guid.NewGuid():N}",
                Role = Role.Coordinator,
                OrganisationId = _fx.OrganisationId,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            db.Users.Add(u);
            return u.Id;
        });

        await using var ctx = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Users/Edit/{userId}");
        response!.Status.Should().Be(200);

        var newFirstName = $"After{Guid.NewGuid():N}";
        await page.FillAsync("#FirstName", newFirstName);
        // Leave password blank -> kept unchanged (controller honours this).
        await page.ClickAsync(SubmitButton);
        await page.WaitForURLAsync("**/Users/Details/**");

        using var db = _fx.Factory.NewDbContext();
        var updated = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
        updated.Should().NotBeNull();
        updated!.FirstName.Should().Be(newFirstName);
    }

    [Fact]
    public async Task Delete_RemovesUser()
    {
        var email = UniqueEmail();
        var userId = _fx.Factory.Seed(db =>
        {
            var u = new CedevaUser
            {
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Doomed",
                LastName = $"Delete{Guid.NewGuid():N}",
                Role = Role.Coordinator,
                OrganisationId = _fx.OrganisationId,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            db.Users.Add(u);
            return u.Id;
        });

        await using var ctx = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Users/Delete/{userId}");
        response!.Status.Should().Be(200);

        await page.ClickAsync(SubmitButton);
        await page.WaitForURLAsync("**/Users**");

        using var db = _fx.Factory.NewDbContext();
        var gone = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
        gone.Should().BeNull("the user should have been deleted");
    }
}
