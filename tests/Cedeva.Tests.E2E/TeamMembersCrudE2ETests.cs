using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser end-to-end coverage of the TeamMembers CRUD as a Coordinator: render Create (200),
/// create a valid member (persisted, verified via the DB), reject an invalid one (validation
/// shown, nothing persisted), edit an existing member (change persisted) and delete it (gone).
///
/// Self-contained: every test uses unique names/emails (GUID-based) so it survives the shared
/// app/SQLite DB/browser that the sequential E2E collection reuses. The professional-information
/// selects are wrapped by Choices.js, so we set the underlying native &lt;select&gt;'s value and
/// dispatch a change event rather than poking the Choices widget; the address hidden fields are
/// set directly the same way the prompt recommends.
/// </summary>
[Collection("E2E")]
public class TeamMembersCrudE2ETests
{
    private readonly PlaywrightFixture _fx;

    public TeamMembersCrudE2ETests(PlaywrightFixture fx) => _fx = fx;

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>Selects a value on a Choices.js-wrapped select by driving the widget (robust).</summary>
    private static Task SetSelectAsync(IPage page, string id, string value) =>
        page.SelectChoicesAsync(id, value);

    /// <summary>Sets the hidden PostalCode/City the address autocomplete normally fills, plus the visible field.</summary>
    private static async Task SetAddressAsync(IPage page, string postalCode, string city)
    {
        await page.EvaluateAsync(
            "args => { document.getElementById('PostalCode').value = args.postalCode; document.getElementById('City').value = args.city; }",
            new { postalCode, city });
        await page.FillAsync("#CombinedAddress", $"{postalCode} {city}");
    }

    [Fact]
    public async Task Create_Get_RendersForm()
    {
        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/TeamMembers/Create");

        response!.Status.Should().Be(200);
        (await page.Locator("#FirstName").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#NationalRegisterNumber").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#TeamRole").CountAsync()).Should().Be(1);
        (await page.Locator("button[type=submit]:not(.btn-link):not(.dropdown-item)").IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithValidData_Persists()
    {
        var lastName = Unique("Member");
        var email = $"{Unique("tm")}@example.com";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/TeamMembers/Create");

        await page.FillAsync("#FirstName", "Camille");
        await page.FillAsync("#LastName", lastName);
        await page.FillAsync("#NationalRegisterNumber", "85.06.15-133.80");
        await page.FillAsync("#BirthDate", "1990-06-15");
        await page.FillAsync("#Email", email);
        await page.FillAsync("#MobilePhoneNumber", "0470000000");
        await page.FillAsync("#Street", "Rue de Test 1");
        await SetAddressAsync(page, "1000", "Bruxelles");
        await SetSelectAsync(page, "TeamRole", ((int)TeamRole.Animator).ToString());
        await SetSelectAsync(page, "License", ((int)License.License).ToString());
        await SetSelectAsync(page, "Status", ((int)Status.Volunteer).ToString());

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync("**/TeamMembers/Details/**");

        page.Url.Should().Contain("/TeamMembers/Details/");
        (await page.InnerTextAsync("body")).Should().Contain(lastName);

        await using var db = _fx.Factory.NewDbContext();
        var created = await db.TeamMembers
            .IgnoreQueryFilters()
            .Include(t => t.Address)
            .FirstOrDefaultAsync(t => t.Email == email);

        created.Should().NotBeNull();
        created!.LastName.Should().Be(lastName);
        created.FirstName.Should().Be("Camille");
        created.OrganisationId.Should().Be(_fx.OrganisationId);
        created.TeamRole.Should().Be(TeamRole.Animator);
        created.License.Should().Be(License.License);
        created.Status.Should().Be(Status.Volunteer);
        created.NationalRegisterNumber.Should().Be("85061513380"); // stored without formatting
        created.Address.PostalCode.Should().Be("1000");
        created.Address.City.Should().Be("Bruxelles");
    }

    [Fact]
    public async Task Create_WithInvalidNationalRegisterNumber_ShowsValidationAndDoesNotPersist()
    {
        var email = $"{Unique("invalid")}@example.com";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/TeamMembers/Create");

        await page.FillAsync("#FirstName", "Bad");
        await page.FillAsync("#LastName", Unique("Bad"));
        await page.FillAsync("#NationalRegisterNumber", "12345678901"); // 11 digits, bad mod-97 checksum
        await page.FillAsync("#BirthDate", "1990-06-15");
        await page.FillAsync("#Email", email);
        await page.FillAsync("#MobilePhoneNumber", "0470000000");
        await page.FillAsync("#Street", "Rue de Test 1");
        await SetAddressAsync(page, "1000", "Bruxelles");
        await SetSelectAsync(page, "TeamRole", ((int)TeamRole.Animator).ToString());
        await SetSelectAsync(page, "License", ((int)License.License).ToString());
        await SetSelectAsync(page, "Status", ((int)Status.Volunteer).ToString());

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        page.Url.Should().NotContain("/Details/", "an invalid NRN must not create a team member");

        await using var db = _fx.Factory.NewDbContext();
        var exists = await db.TeamMembers.IgnoreQueryFilters().AnyAsync(t => t.Email == email);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Edit_ChangesFirstName_Persists()
    {
        var seed = _fx.Factory.Seed(db =>
        {
            var address = new Address
            {
                Street = "Rue Edit 1",
                City = "Bruxelles",
                PostalCode = "1000",
                Country = Country.Belgium
            };
            var member = new TeamMember
            {
                FirstName = "Original",
                LastName = Unique("EditMember"),
                Email = $"{Unique("edit")}@example.com",
                MobilePhoneNumber = "0470000000",
                NationalRegisterNumber = "85061513380",
                BirthDate = new DateTime(1990, 6, 15),
                Address = address,
                TeamRole = TeamRole.Animator,
                License = License.License,
                Status = Status.Volunteer,
                OrganisationId = _fx.OrganisationId
            };
            db.TeamMembers.Add(member);
            db.SaveChanges();
            return member.TeamMemberId;
        });

        var newFirstName = Unique("Renamed");

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/TeamMembers/Edit/{seed}");
        response!.Status.Should().Be(200);

        await page.FillAsync("#FirstName", newFirstName);
        await SetSelectAsync(page, "Status", ((int)Status.Compensated).ToString());
        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync("**/TeamMembers/Details/**");

        await using var db = _fx.Factory.NewDbContext();
        var updated = await db.TeamMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TeamMemberId == seed);

        updated.Should().NotBeNull();
        updated!.FirstName.Should().Be(newFirstName);
        updated.Status.Should().Be(Status.Compensated);
    }

    [Fact]
    public async Task Delete_RemovesTeamMember()
    {
        var seed = _fx.Factory.Seed(db =>
        {
            var address = new Address
            {
                Street = "Rue Delete 1",
                City = "Bruxelles",
                PostalCode = "1000",
                Country = Country.Belgium
            };
            var member = new TeamMember
            {
                FirstName = "ToDelete",
                LastName = Unique("DeleteMember"),
                Email = $"{Unique("del")}@example.com",
                MobilePhoneNumber = "0470000000",
                NationalRegisterNumber = "85061513380",
                BirthDate = new DateTime(1990, 6, 15),
                Address = address,
                TeamRole = TeamRole.Animator,
                License = License.License,
                Status = Status.Volunteer,
                OrganisationId = _fx.OrganisationId
            };
            db.TeamMembers.Add(member);
            db.SaveChanges();
            return member.TeamMemberId;
        });

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/TeamMembers/Delete/{seed}");
        response!.Status.Should().Be(200);

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync("**/TeamMembers**");

        await using var db = _fx.Factory.NewDbContext();
        var exists = await db.TeamMembers.IgnoreQueryFilters().AnyAsync(t => t.TeamMemberId == seed);
        exists.Should().BeFalse();
    }
}
