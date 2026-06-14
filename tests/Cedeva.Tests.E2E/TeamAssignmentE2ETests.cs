using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// End-to-end coverage of the Coordinator-driven team assignment flow under ActivityManagement:
/// rendering the TeamMembers page, assigning an available team member to an activity (Add), and
/// removing an assigned one (Remove). Each test seeds its own activity + team member (unique names)
/// in the fixture organisation and verifies the activity↔team-member link via a fresh DbContext.
/// </summary>
[Collection("E2E")]
public class TeamAssignmentE2ETests
{
    private readonly PlaywrightFixture _fx;

    public TeamAssignmentE2ETests(PlaywrightFixture fx) => _fx = fx;

    private sealed record Seeded(int ActivityId, int TeamMemberId, string MarkerLastName, string FullName);

    /// <summary>Seeds a fresh activity + team member in the fixture org with a unique marker name.</summary>
    private Seeded SeedActivityAndTeamMember(bool preAssign)
    {
        return _fx.Factory.Seed(ctx =>
        {
            var token = Guid.NewGuid().ToString("N")[..8];
            var lastName = $"Teamster-{token}";
            var firstName = "Alex";

            var teamMember = new TeamMember
            {
                FirstName = firstName,
                LastName = lastName,
                Email = $"tm-{token}@e2e.test",
                BirthDate = new DateTime(1990, 1, 1),
                MobilePhoneNumber = "0470000000",
                NationalRegisterNumber = "90.01.01-001.01",
                TeamRole = TeamRole.Animator,
                License = License.License,
                Status = Status.Volunteer,
                OrganisationId = _fx.OrganisationId,
                Address = new Address
                {
                    Street = "Rue E2E 1",
                    City = "Bruxelles",
                    PostalCode = "1000",
                    Country = Country.Belgium
                }
            };

            var activity = new Activity
            {
                Name = $"Stage-{token}",
                Description = "Activite team-assign E2E",
                IsActive = true,
                PricePerDay = 20m,
                StartDate = DateTime.Now.AddMonths(2),
                EndDate = DateTime.Now.AddMonths(2).AddDays(4),
                OrganisationId = _fx.OrganisationId
            };

            if (preAssign)
                activity.TeamMembers.Add(teamMember);

            ctx.Add(teamMember);
            ctx.Add(activity);
            ctx.SaveChanges();

            return new Seeded(activity.Id, teamMember.TeamMemberId, lastName, $"{firstName} {lastName}");
        });
    }

    [Fact]
    public async Task TeamMembersPage_RendersForCoordinator()
    {
        var seeded = SeedActivityAndTeamMember(preAssign: false);

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/ActivityManagement/TeamMembers?id={seeded.ActivityId}");

        response!.Status.Should().Be(200);
        // The unassigned member shows up in the "Available" panel.
        (await page.InnerTextAsync("body")).Should().Contain(seeded.MarkerLastName);
    }

    [Fact]
    public async Task AddTeamMember_LinksMemberToActivity_AndPersists()
    {
        var seeded = SeedActivityAndTeamMember(preAssign: false);

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/ActivityManagement/TeamMembers?id={seeded.ActivityId}");

        // The "Add" submit button lives in the available-member list-group-item that shows the name.
        var addButton = page.Locator($".list-group-item:has-text(\"{seeded.MarkerLastName}\") button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await addButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 7000 });
        await addButton.ClickAsync();

        await page.WaitForURLAsync("**/ActivityManagement/TeamMembers**");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Persisted link verified independently of the UI.
        await using var db = _fx.Factory.NewDbContext();
        var activity = await db.Activities
            .IgnoreQueryFilters()
            .Include(a => a.TeamMembers)
            .FirstAsync(a => a.Id == seeded.ActivityId);
        activity.TeamMembers.Should().ContainSingle(tm => tm.TeamMemberId == seeded.TeamMemberId);
    }

    [Fact]
    public async Task RemoveTeamMember_UnlinksMemberFromActivity_AndPersists()
    {
        var seeded = SeedActivityAndTeamMember(preAssign: true);

        // Sanity: it starts assigned.
        await using (var pre = _fx.Factory.NewDbContext())
        {
            var act = await pre.Activities
                .IgnoreQueryFilters()
                .Include(a => a.TeamMembers)
                .FirstAsync(a => a.Id == seeded.ActivityId);
            act.TeamMembers.Should().Contain(tm => tm.TeamMemberId == seeded.TeamMemberId);
        }

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/ActivityManagement/TeamMembers?id={seeded.ActivityId}");

        // The "Remove" submit button lives in the assigned-member table row that shows the name.
        // confirm() dialog must be accepted for the form to submit.
        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

        var removeButton = page.Locator($"tr:has-text(\"{seeded.MarkerLastName}\") button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await removeButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 7000 });
        await removeButton.ClickAsync();

        await page.WaitForURLAsync("**/ActivityManagement/TeamMembers**");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await using var db = _fx.Factory.NewDbContext();
        var activity = await db.Activities
            .IgnoreQueryFilters()
            .Include(a => a.TeamMembers)
            .FirstAsync(a => a.Id == seeded.ActivityId);
        activity.TeamMembers.Should().NotContain(tm => tm.TeamMemberId == seeded.TeamMemberId);
    }

    [Fact]
    public async Task TeamMembers_WithUnknownActivityId_ReturnsNotFound()
    {
        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/ActivityManagement/TeamMembers?id=99999999");

        response!.Status.Should().Be(404);
    }
}
