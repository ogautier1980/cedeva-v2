using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser CRUD coverage for the coordinator-scoped Parents area. Drives the real Razor forms
/// (anti-forgery token, jQuery validation, address autocomplete hidden fields) end-to-end and
/// asserts persisted state via a fresh DbContext. Every test uses unique names/emails so the
/// shared sequential E2E DB stays interference-free.
/// </summary>
[Collection("E2E")]
public class ParentsCrudE2ETests
{
    private readonly PlaywrightFixture _fx;

    public ParentsCrudE2ETests(PlaywrightFixture fx) => _fx = fx;

    // Sets the hidden PostalCode/City fields (normally populated by the autocomplete) and shows a
    // matching display value in the combined-address box, then fills the visible street/contact
    // inputs. Mirrors what a coordinator does on the Create/Edit forms.
    private static async Task FillAddressAsync(IPage page)
    {
        await page.FillAsync("#Street", "Rue de la Loi 16");
        await page.FillAsync("#CombinedAddress", "1000 Bruxelles");
        await page.EvaluateAsync(
            "() => { document.getElementById('PostalCode').value='1000'; document.getElementById('City').value='Bruxelles'; }");
    }

    [Fact]
    public async Task CreateForm_RendersOk()
    {
        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Parents/Create");

        response!.Status.Should().Be(200);
        (await page.Locator("#FirstName").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#NationalRegisterNumber").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("button[type=submit]:not(.btn-link):not(.dropdown-item)").IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithValidData_PersistsParent()
    {
        var last = $"Dupont{Guid.NewGuid():N}";
        var email = $"parent.{Guid.NewGuid():N}@e2e.test";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Parents/Create");
        response!.Status.Should().Be(200);

        await page.FillAsync("#FirstName", "Jean");
        await page.FillAsync("#LastName", last);
        await page.FillAsync("#Email", email);
        await page.FillAsync("#MobilePhoneNumber", "0470000000");
        await page.FillAsync("#NationalRegisterNumber", "85.06.15-133.80");
        await FillAddressAsync(page);

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The controller strips NRN formatting before persisting.
        await using var db = _fx.Factory.NewDbContext();
        var parent = await db.Parents.IgnoreQueryFilters()
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Email == email);

        parent.Should().NotBeNull();
        parent!.FirstName.Should().Be("Jean");
        parent.LastName.Should().Be(last);
        parent.MobilePhoneNumber.Should().Be("0470000000");
        parent.NationalRegisterNumber.Should().Be("85061513380");
        parent.OrganisationId.Should().Be(_fx.OrganisationId);
        parent.Address.Should().NotBeNull();
        parent.Address!.PostalCode.Should().Be("1000");
        parent.Address.City.Should().Be("Bruxelles");
    }

    [Fact]
    public async Task Create_WithInvalidNrn_ShowsValidationError_AndDoesNotPersist()
    {
        var email = $"parent.{Guid.NewGuid():N}@e2e.test";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Parents/Create");
        response!.Status.Should().Be(200);

        await page.FillAsync("#FirstName", "Marie");
        await page.FillAsync("#LastName", $"Invalid{Guid.NewGuid():N}");
        await page.FillAsync("#Email", email);
        await page.FillAsync("#MobilePhoneNumber", "0470000000");
        // Wrong mod-97 check digits -> ValidNationalRegisterNumber rejects it.
        await page.FillAsync("#NationalRegisterNumber", "85.06.15-133.99");
        await FillAddressAsync(page);

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Stayed on the Create form (server re-rendered with the model error).
        page.Url.Should().Contain("/Parents/Create");
        var nrnError = page.Locator("span[data-valmsg-for='NationalRegisterNumber']");
        (await nrnError.InnerTextAsync()).Trim().Should().NotBeEmpty();

        await using var db = _fx.Factory.NewDbContext();
        (await db.Parents.IgnoreQueryFilters().AnyAsync(p => p.Email == email))
            .Should().BeFalse("an invalid NRN must not persist the parent");
    }

    [Fact]
    public async Task Edit_ChangesField_Persists()
    {
        var email = $"parent.{Guid.NewGuid():N}@e2e.test";
        var originalLast = $"Original{Guid.NewGuid():N}";
        var newLast = $"Updated{Guid.NewGuid():N}";

        // Seed the parent directly so the edit flow is isolated from the create flow.
        var parentId = _fx.Factory.Seed(db =>
        {
            var parent = new Cedeva.Core.Entities.Parent
            {
                FirstName = "Edit",
                LastName = originalLast,
                Email = email,
                MobilePhoneNumber = "0470000000",
                NationalRegisterNumber = "85061513380",
                OrganisationId = _fx.OrganisationId,
                Address = new Cedeva.Core.Entities.Address
                {
                    Street = "Rue Ancienne 1",
                    City = "Bruxelles",
                    PostalCode = "1000",
                    Country = Cedeva.Core.Enums.Country.Belgium
                }
            };
            db.Parents.Add(parent);
            db.SaveChanges();
            return parent.Id;
        });

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Parents/Edit/{parentId}");
        response!.Status.Should().Be(200);

        await page.FillAsync("#LastName", newLast);
        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await using var db = _fx.Factory.NewDbContext();
        var updated = await db.Parents.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == parentId);
        updated.Should().NotBeNull();
        updated!.LastName.Should().Be(newLast);
    }

    [Fact]
    public async Task Delete_RemovesParent()
    {
        var email = $"parent.{Guid.NewGuid():N}@e2e.test";

        var parentId = _fx.Factory.Seed(db =>
        {
            var parent = new Cedeva.Core.Entities.Parent
            {
                FirstName = "Delete",
                LastName = $"Me{Guid.NewGuid():N}",
                Email = email,
                MobilePhoneNumber = "0470000000",
                NationalRegisterNumber = "85061513380",
                OrganisationId = _fx.OrganisationId,
                Address = new Cedeva.Core.Entities.Address
                {
                    Street = "Rue a Supprimer 1",
                    City = "Bruxelles",
                    PostalCode = "1000",
                    Country = Cedeva.Core.Enums.Country.Belgium
                }
            };
            db.Parents.Add(parent);
            db.SaveChanges();
            return parent.Id;
        });

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/Parents/Delete/{parentId}");
        response!.Status.Should().Be(200);

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await using var db = _fx.Factory.NewDbContext();
        (await db.Parents.IgnoreQueryFilters().AnyAsync(p => p.Id == parentId))
            .Should().BeFalse("the parent should be removed after confirming deletion");
    }
}
