using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser CRUD coverage for the admin-only Organisations area. Drives the real Create/Edit/Delete
/// Razor forms with Chromium and asserts persisted state via a fresh DbContext (IgnoreQueryFilters
/// because this scope carries no organisation). Every test uses a unique name to stay isolated
/// inside the shared sequential E2E collection.
///
/// Note: the OrganisationsController Create/Edit POST only persists Name, Description and the
/// Address — it never copies BankAccountNumber/BankAccountName onto the entity. The forms are
/// filled with those fields (they exist in the view) but persistence is asserted only on the
/// fields the controller actually saves.
/// </summary>
[Collection("E2E")]
public class OrganisationsCrudE2ETests
{
    private readonly PlaywrightFixture _fx;

    public OrganisationsCrudE2ETests(PlaywrightFixture fx) => _fx = fx;

    private static Task<IPage> NewAdminPageAsync(IBrowserContext context) => context.NewPageAsync();

    private static async Task SetPostalCodeAndCityAsync(IPage page, string postalCode, string city)
    {
        // The combined autocomplete normally fills these hidden inputs; set them directly so the
        // form posts a valid address without depending on the AddressApi round-trip.
        await page.EvaluateAsync(
            "([pc, city]) => { document.getElementById('PostalCode').value = pc; document.getElementById('City').value = city; }",
            new[] { postalCode, city });
    }

    [Fact]
    public async Task Create_RendersForm_ReturnsOk()
    {
        await using var context = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await NewAdminPageAsync(context);

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Organisations/Create");

        response!.Status.Should().Be(200);
        (await page.Locator("#Name").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#Description").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#BankAccountNumber").IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithValidData_PersistsOrganisation()
    {
        var name = $"Org-{Guid.NewGuid():N}";
        var description = "Organisation de test E2E creee via le navigateur.";

        await using var context = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await NewAdminPageAsync(context);

        await page.GotoAsync($"{_fx.BaseUrl}/Organisations/Create");
        await page.FillAsync("#Name", name);
        await page.FillAsync("#Description", description);
        await page.FillAsync("#Street", "Rue de Test 10");
        await page.FillAsync("#CombinedAddress", "1000 Bruxelles");
        await SetPostalCodeAndCityAsync(page, "1000", "Bruxelles");
        await page.FillAsync("#BankAccountNumber", "BE68539007547034");
        await page.FillAsync("#BankAccountName", name);

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync("**/Organisations/Details/**");

        // Confirm landing on the details page for the created organisation.
        page.Url.Should().Contain("/Organisations/Details/");
        (await page.InnerTextAsync("body")).Should().Contain(name);

        await using var db = _fx.Factory.NewDbContext();
        var saved = await db.Organisations
            .IgnoreQueryFilters()
            .Include(o => o.Address)
            .FirstOrDefaultAsync(o => o.Name == name);

        saved.Should().NotBeNull();
        saved!.Description.Should().Be(description);
        saved.Address.Should().NotBeNull();
        saved.Address.Street.Should().Be("Rue de Test 10");
        saved.Address.PostalCode.Should().Be("1000");
        saved.Address.City.Should().Be("Bruxelles");
    }

    [Fact]
    public async Task Create_WithMissingName_ShowsValidationError_NotPersisted()
    {
        var description = "Description valide mais sans nom pour declencher la validation E2E.";

        await using var context = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await NewAdminPageAsync(context);

        await page.GotoAsync($"{_fx.BaseUrl}/Organisations/Create");
        // Leave #Name empty to trigger the [Required] validation.
        await page.FillAsync("#Description", description);
        await page.FillAsync("#Street", "Rue Invalide 1");
        await page.FillAsync("#CombinedAddress", "1000 Bruxelles");
        await SetPostalCodeAndCityAsync(page, "1000", "Bruxelles");

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Stayed on the Create form (server re-render or client validation) — never reached Details.
        page.Url.Should().NotContain("/Organisations/Details/");
        var nameError = page.Locator("span[data-valmsg-for='Name']");
        (await nameError.InnerTextAsync()).Trim().Should().NotBeNullOrEmpty();

        await using var db = _fx.Factory.NewDbContext();
        var exists = await db.Organisations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Description == description);
        exists.Should().BeFalse("an organisation without a name must not be persisted");
    }

    [Fact]
    public async Task Edit_ChangesName_PersistsUpdate()
    {
        var originalName = $"Org-{Guid.NewGuid():N}";
        var updatedName = $"Org-Upd-{Guid.NewGuid():N}";

        // Seed an organisation with its own address so we have something to edit.
        var orgId = _fx.Factory.Seed(ctx =>
        {
            var org = new Core.Entities.Organisation
            {
                Name = originalName,
                Description = "Organisation a editer via le navigateur E2E.",
                Address = new Core.Entities.Address
                {
                    Street = "Rue Origine 5",
                    City = "Bruxelles",
                    PostalCode = "1000",
                    Country = Core.Enums.Country.Belgium
                }
            };
            ctx.Organisations.Add(org);
            ctx.SaveChanges();
            return org.Id;
        });

        await using var context = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await NewAdminPageAsync(context);

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Organisations/Edit/{orgId}");
        response!.Status.Should().Be(200);

        await page.FillAsync("#Name", updatedName);
        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync($"**/Organisations/Details/{orgId}**");

        (await page.InnerTextAsync("body")).Should().Contain(updatedName);

        await using var db = _fx.Factory.NewDbContext();
        var saved = await db.Organisations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == orgId);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be(updatedName);
    }

    [Fact]
    public async Task Delete_RemovesOrganisation()
    {
        var name = $"Org-Del-{Guid.NewGuid():N}";

        var orgId = _fx.Factory.Seed(ctx =>
        {
            var org = new Core.Entities.Organisation
            {
                Name = name,
                Description = "Organisation a supprimer via le navigateur E2E.",
                Address = new Core.Entities.Address
                {
                    Street = "Rue A Supprimer 9",
                    City = "Bruxelles",
                    PostalCode = "1000",
                    Country = Core.Enums.Country.Belgium
                }
            };
            ctx.Organisations.Add(org);
            ctx.SaveChanges();
            return org.Id;
        });

        await using var context = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await NewAdminPageAsync(context);

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Organisations/Delete/{orgId}");
        response!.Status.Should().Be(200);
        (await page.InnerTextAsync("body")).Should().Contain(name);

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync("**/Organisations**");

        await using var db = _fx.Factory.NewDbContext();
        var exists = await db.Organisations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Id == orgId);
        exists.Should().BeFalse("the organisation should be gone after deletion");
    }
}
