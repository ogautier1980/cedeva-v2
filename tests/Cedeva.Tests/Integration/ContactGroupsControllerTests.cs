using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// CRUD coverage for saved contact groups: create persists the chosen members (filtered to real
/// org contacts), edit replaces them, delete cascades, and a group with no valid members is rejected.
/// </summary>
[Collection("WebApp")]
public class ContactGroupsControllerTests
{
    private static int SeedOrgWithContacts(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            ctx.Contacts.AddRange(
                new Contact { Organisation = org, FirstName = "Sophie", LastName = "Lemaire", Email = "a@test.be" },
                new Contact { Organisation = org, FirstName = "Marc", LastName = "Dewitte", Email = "b@test.be" },
                new Contact { Organisation = org, FirstName = "Cathy", LastName = "Renard", Email = "c@test.be" });
            return 0;
        });
        return org.Id;
    }

    [Fact]
    public async Task Create_PersistsGroupWithSelectedMembers_FilteringUnknownEmails()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedOrgWithContacts(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ContactGroups/Create", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Name", "Équipe soignante"),
            new KeyValuePair<string, string>("SelectedEmails", "a@test.be"),
            new KeyValuePair<string, string>("SelectedEmails", "c@test.be"),
            new KeyValuePair<string, string>("SelectedEmails", "stranger@evil.be"), // not a contact -> dropped
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var group = await db.ContactGroups.IgnoreQueryFilters().Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Name == "Équipe soignante");
        group.Should().NotBeNull();
        group!.OrganisationId.Should().Be(orgId);
        group.Members.Select(m => m.Email).Should().BeEquivalentTo(new[] { "a@test.be", "c@test.be" });
    }

    [Fact]
    public async Task Create_WithNoValidMembers_ReturnsViewWithError()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedOrgWithContacts(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ContactGroups/Create", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Name", "Vide"),
            new KeyValuePair<string, string>("SelectedEmails", "stranger@evil.be"),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "no valid member -> re-render with error");
        using var db = factory.NewDbContext();
        (await db.ContactGroups.IgnoreQueryFilters().AnyAsync(g => g.Name == "Vide")).Should().BeFalse();
    }

    [Fact]
    public async Task Edit_ReplacesMembers()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedOrgWithContacts(factory);
        var groupId = SeedGroupId(factory, orgId);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ContactGroups/Edit", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Id", groupId.ToString()),
            new KeyValuePair<string, string>("Name", "G1 renommé"),
            new KeyValuePair<string, string>("SelectedEmails", "b@test.be"),
            new KeyValuePair<string, string>("SelectedEmails", "c@test.be"),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var db = factory.NewDbContext();
        var group = await db.ContactGroups.IgnoreQueryFilters().Include(g => g.Members).FirstAsync(g => g.Id == groupId);
        group.Name.Should().Be("G1 renommé");
        group.Members.Select(m => m.Email).Should().BeEquivalentTo(new[] { "b@test.be", "c@test.be" });
    }

    [Fact]
    public async Task Delete_RemovesGroupAndMembers()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedOrgWithContacts(factory);
        var groupId = SeedGroupId(factory, orgId);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync($"/ContactGroups/Delete/{groupId}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var db = factory.NewDbContext();
        (await db.ContactGroups.IgnoreQueryFilters().AnyAsync(g => g.Id == groupId)).Should().BeFalse();
        (await db.ContactGroupMembers.IgnoreQueryFilters().AnyAsync(m => m.ContactGroupId == groupId)).Should().BeFalse();
    }

    private static int SeedGroupId(CedevaWebApplicationFactory factory, int orgId)
    {
        using var db = factory.NewDbContext();
        var existing = db.ContactGroups.IgnoreQueryFilters().Where(g => g.OrganisationId == orgId).Select(g => g.Id).FirstOrDefault();
        if (existing != 0) return existing;
        return factory.Seed(ctx =>
        {
            var g = new ContactGroup { OrganisationId = orgId, Name = "G1", Members = { new ContactGroupMember { Email = "a@test.be" } } };
            ctx.ContactGroups.Add(g);
            ctx.SaveChanges();
            return g.Id;
        });
    }
}
