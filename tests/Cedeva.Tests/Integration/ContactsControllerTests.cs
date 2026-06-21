using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Integration coverage for the Contacts directory: the Index lists an org's contacts by function,
/// and "other contacts" can be created/edited/deleted (org-scoped).
/// </summary>
[Collection("WebApp")]
public class ContactsControllerTests
{
    private static int SeedOrgWithContacts(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            ctx.Contacts.Add(new Contact
            {
                Organisation = org, FirstName = "Sophie", LastName = "Lemaire",
                Email = "dr.lemaire@test.be", Function = "Médecin"
            });
            return 0;
        });
        return org.Id;
    }

    [Fact]
    public async Task Index_ReturnsOk_ForCoordinator()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedOrgWithContacts(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync("/Contacts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Lemaire", "the seeded other-contact should be listed");
    }

    [Fact]
    public async Task Create_PersistsOtherContact()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedOrgWithContacts(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/Contacts/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Marc",
            ["LastName"] = "Dewitte",
            ["Email"] = "marc.dewitte@test.be",
            ["Function"] = "Traiteur",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var contact = await db.Contacts.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.LastName == "Dewitte");
        contact.Should().NotBeNull();
        contact!.OrganisationId.Should().Be(orgId);
        contact.Function.Should().Be("Traiteur");
    }

    [Fact]
    public async Task Delete_RemovesOtherContact()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = SeedOrgWithContacts(factory);
        int contactId;
        using (var db = factory.NewDbContext())
        {
            contactId = await db.Contacts.IgnoreQueryFilters().Where(c => c.OrganisationId == orgId).Select(c => c.Id).FirstAsync();
        }
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync($"/Contacts/Delete/{contactId}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        using var verify = factory.NewDbContext();
        (await verify.Contacts.IgnoreQueryFilters().AnyAsync(c => c.Id == contactId)).Should().BeFalse();
    }
}
