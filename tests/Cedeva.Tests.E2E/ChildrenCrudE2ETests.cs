using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser E2E coverage for the Children CRUD area, driven as a Coordinator scoped to the seeded
/// organisation. Each test seeds its own parent (and child) with a unique marker so the shared
/// SQLite DB and sequential collection cannot cross-contaminate. Persistence is asserted through a
/// fresh DbContext (IgnoreQueryFilters, since that scope has no tenant).
/// </summary>
[Collection("E2E")]
public class ChildrenCrudE2ETests
{
    // Valid mod-97 child NRN (16.07.08-164.10) and parent NRN (85.06.15-133.80).
    private const string ChildNrn = "16.07.08-164.10";
    private const string ParentNrn = "85.06.15-133.80";

    private readonly PlaywrightFixture _fx;

    public ChildrenCrudE2ETests(PlaywrightFixture fx) => _fx = fx;

    /// <summary>Seeds a parent in the test organisation and returns its id, using a unique surname
    /// so the created child can be located unambiguously.</summary>
    private int SeedParent(string marker)
    {
        return _fx.Factory.Seed(ctx =>
        {
            var parent = new Parent
            {
                FirstName = "Pat",
                LastName = marker,
                Email = $"parent-{Guid.NewGuid():N}@e2e.test",
                MobilePhoneNumber = "0470000000",
                NationalRegisterNumber = ParentNrn.Replace(".", "").Replace("-", ""),
                OrganisationId = _fx.OrganisationId,
                Address = new Address
                {
                    Street = "Rue E2E",
                    City = "Bruxelles",
                    PostalCode = "1000",
                    Country = Country.Belgium
                }
            };
            ctx.Parents.Add(parent);
            ctx.SaveChanges();
            return parent.Id;
        });
    }

    [Fact]
    public async Task Create_Get_RendersForm()
    {
        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Children/Create");

        response!.Status.Should().Be(200);
        (await page.Locator("#FirstName").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#LastName").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#NationalRegisterNumber").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#BirthDate").IsVisibleAsync()).Should().BeTrue();
        // Parent picker exists (rendered as a select, enhanced by Choices.js).
        (await page.Locator("#ParentId").CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_WithExistingParent_PersistsChild()
    {
        var parentMarker = $"ParentExisting-{Guid.NewGuid():N}";
        var parentId = SeedParent(parentMarker);
        var childLastName = $"ChildCreate-{Guid.NewGuid():N}";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Children/Create");

        await page.FillAsync("#FirstName", "Lou");
        await page.FillAsync("#LastName", childLastName);
        await page.FillAsync("#NationalRegisterNumber", ChildNrn);
        await page.FillAsync("#BirthDate", "2016-07-08");
        // Choices.js leaves the underlying <select> in the DOM; select the seeded parent by value.
        await page.SelectChoicesAsync("#ParentId", parentId.ToString());

        await page.ClickAsync("button.btn-primary.text-nowrap[type=submit]");
        await page.WaitForURLAsync("**/Children/Details/**");

        // Landing page is the child's Details page; assert on the unique seeded surname.
        (await page.Locator($"text={childLastName}").First.IsVisibleAsync()).Should().BeTrue();

        await using var db = _fx.Factory.NewDbContext();
        var child = await db.Children.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.LastName == childLastName);
        child.Should().NotBeNull();
        child!.FirstName.Should().Be("Lou");
        child.ParentId.Should().Be(parentId);
        // NRN is stored stripped of formatting.
        child.NationalRegisterNumber.Should().Be(ChildNrn.Replace(".", "").Replace("-", ""));
        child.BirthDate.Date.Should().Be(new DateTime(2016, 7, 8));
    }

    [Fact]
    public async Task Create_Invalid_ShowsValidationError_AndDoesNotPersist()
    {
        var parentMarker = $"ParentInvalid-{Guid.NewGuid():N}";
        var parentId = SeedParent(parentMarker);
        var childLastName = $"ChildInvalid-{Guid.NewGuid():N}";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Children/Create");

        await page.FillAsync("#FirstName", "Lou");
        await page.FillAsync("#LastName", childLastName);
        // Invalid NRN: right length range but fails the mod-97 check digit.
        await page.FillAsync("#NationalRegisterNumber", "16.07.08-164.11");
        await page.FillAsync("#BirthDate", "2016-07-08");
        await page.SelectChoicesAsync("#ParentId", parentId.ToString());

        await page.ClickAsync("button.btn-primary.text-nowrap[type=submit]");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Stayed on the Create form (no redirect to Details) and an error is shown for the NRN field.
        page.Url.Should().Contain("/Children/Create");
        var nrnError = page.Locator("span[data-valmsg-for='NationalRegisterNumber'], span.text-danger").First;
        (await page.Locator(".text-danger:not(:empty), .field-validation-error").CountAsync())
            .Should().BeGreaterThan(0, "the invalid NRN should surface a validation message");

        await using var db = _fx.Factory.NewDbContext();
        var exists = await db.Children.IgnoreQueryFilters().AnyAsync(c => c.LastName == childLastName);
        exists.Should().BeFalse("an invalid child must not be persisted");
    }

    [Fact]
    public async Task Edit_ChangesFirstName_Persisted()
    {
        var parentMarker = $"ParentEdit-{Guid.NewGuid():N}";
        var parentId = SeedParent(parentMarker);
        var childLastName = $"ChildEdit-{Guid.NewGuid():N}";

        var childId = _fx.Factory.Seed(db =>
        {
            var child = new Child
            {
                FirstName = "OldName",
                LastName = childLastName,
                NationalRegisterNumber = ChildNrn.Replace(".", "").Replace("-", ""),
                BirthDate = new DateTime(2016, 7, 8),
                ParentId = parentId
            };
            db.Children.Add(child);
            db.SaveChanges();
            return child.Id;
        });

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/Children/Edit/{childId}");
        response!.Status.Should().Be(200);

        await page.FillAsync("#FirstName", "NewName");
        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await using var db = _fx.Factory.NewDbContext();
        var child = await db.Children.IgnoreQueryFilters().FirstAsync(c => c.Id == childId);
        child.FirstName.Should().Be("NewName");
        child.ParentId.Should().Be(parentId);
    }

    [Fact]
    public async Task Delete_RemovesChild()
    {
        var parentMarker = $"ParentDelete-{Guid.NewGuid():N}";
        var parentId = SeedParent(parentMarker);
        var childLastName = $"ChildDelete-{Guid.NewGuid():N}";

        var childId = _fx.Factory.Seed(db =>
        {
            var child = new Child
            {
                FirstName = "ToDelete",
                LastName = childLastName,
                NationalRegisterNumber = ChildNrn.Replace(".", "").Replace("-", ""),
                BirthDate = new DateTime(2016, 7, 8),
                ParentId = parentId
            };
            db.Children.Add(child);
            db.SaveChanges();
            return child.Id;
        });

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/Children/Delete/{childId}");
        response!.Status.Should().Be(200);

        // The delete confirmation page posts back to Delete; the only submit button confirms.
        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync("**/Children**");

        await using var db = _fx.Factory.NewDbContext();
        var exists = await db.Children.IgnoreQueryFilters().AnyAsync(c => c.Id == childId);
        exists.Should().BeFalse("the child should be gone after delete");
    }

    // NOTE: The Children Create view DOES expose an inline "create new parent" form, but it is a
    // separate <form id="parentForm"> submitted via AJAX to Parents/CreateAjax (it does not post a
    // nested parent together with the child). The child itself is always linked to an already-
    // persisted parent through the #ParentId select. The nested-parent-with-child single-submit
    // flow therefore does not exist in the UI and is intentionally not asserted here; the AJAX
    // parent-creation path is covered by the Parents area tests.
}
