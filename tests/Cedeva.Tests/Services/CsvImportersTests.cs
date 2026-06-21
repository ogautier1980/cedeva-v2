using System.Text;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Services.Email;
using Cedeva.Infrastructure.Services.Import;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Services;

/// <summary>
/// Coverage for the entity CSV importers (team members, contacts, organisations, activities) and the
/// tenancy guarantee that an import only writes into the organisation it is given.
/// </summary>
public class CsvImportersTests
{
    private static (SqliteTestContext Db, int OrgA, int OrgB) NewDb()
    {
        var db = new SqliteTestContext();
        var a = TestData.Organisation("Org A");
        var b = TestData.Organisation("Org B");
        using var seed = db.NewContext(FakeCurrentUserService.Admin());
        seed.AddRange(a, b);
        seed.SaveChanges();
        return (db, a.Id, b.Id);
    }

    private static Stream Csv(params string[] lines) =>
        new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

    [Fact]
    public async Task TeamMembers_ValidRows_CreatesMembers_AndDeduplicatesByEmail()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgA));
        var sut = new TeamMemberCsvImporter(ctx);

        var result = await sut.ImportAsync(Csv(
            "FirstName;LastName;Email;BirthDate;MobilePhone;NRN;Street;PostalCode;City;Role",
            "Anne;Leroy;anne@test.be;10/05/1990;0470000000;;Rue A 1;1000;Bruxelles;Coordinateur",
            "Anne;Leroy;anne@test.be;10/05/1990;0470000000;;Rue A 1;1000;Bruxelles;Animateur"), orgA);

        result.Created.Should().Be(1);
        result.Skipped.Should().Be(1, "the second row repeats the email");

        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        var member = await verify.TeamMembers.IgnoreQueryFilters().Include(t => t.Address).FirstAsync(t => t.Email == "anne@test.be");
        member.OrganisationId.Should().Be(orgA);
        member.TeamRole.Should().Be(TeamRole.Coordinator);
        member.Address.City.Should().Be("Bruxelles");
    }

    [Fact]
    public async Task Contacts_ValidRows_CreatesContacts()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgA));
        var sut = new ContactCsvImporter(ctx);

        var result = await sut.ImportAsync(Csv(
            "FirstName;LastName;Email;Phone;Function",
            "Dr;House;house@test.be;02 000;Médecin",
            "Le;Traiteur;;0470;Traiteur"), orgA);

        result.Created.Should().Be(2);
        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        (await verify.Contacts.IgnoreQueryFilters().CountAsync(c => c.OrganisationId == orgA)).Should().Be(2);
    }

    [Fact]
    public async Task Organisations_ValidRows_CreatesOrganisations_DedupByName()
    {
        var (db, _, _) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Admin());
        var sut = new OrganisationCsvImporter(ctx);

        var result = await sut.ImportAsync(Csv(
            "Name;Description;Street;PostalCode;City;Email",
            "Nouvelle ASBL;Centre de vacances;Rue Z 9;5000;Namur;info@asbl.be",
            "Org A;Doublon;Rue X 1;1000;Bruxelles;"), organisationId: 0);

        result.Created.Should().Be(1, "Org A already exists");
        result.Skipped.Should().Be(1);
        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        (await verify.Organisations.IgnoreQueryFilters().AnyAsync(o => o.Name == "Nouvelle ASBL")).Should().BeTrue();
    }

    [Fact]
    public async Task Activities_ValidRow_CreatesActivityWithDays()
    {
        var (db, orgA, _) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgA));
        var sut = new ActivityCsvImporter(ctx, new EmailTemplateService(ctx));

        var result = await sut.ImportAsync(Csv(
            "Name;Description;StartDate;EndDate;PricePerDay;IsActive",
            "Stage Été;Plaine de jeux;06/07/2026;10/07/2026;20;true"), orgA);

        result.Created.Should().Be(1);
        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        var activity = await verify.Activities.IgnoreQueryFilters().Include(a => a.Days).FirstAsync(a => a.Name == "Stage Été");
        activity.OrganisationId.Should().Be(orgA);
        activity.Days.Should().HaveCount(5, "one day per date in the range 06->10 July");
    }

    [Fact]
    public async Task Import_WritesOnlyIntoTheGivenOrganisation()
    {
        var (db, orgA, orgB) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgA));
        var sut = new ContactCsvImporter(ctx);

        await sut.ImportAsync(Csv(
            "FirstName;LastName;Email;Phone;Function",
            "Iso;Lated;iso@test.be;;"), orgA);

        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        (await verify.Contacts.IgnoreQueryFilters().CountAsync(c => c.OrganisationId == orgA)).Should().Be(1);
        (await verify.Contacts.IgnoreQueryFilters().CountAsync(c => c.OrganisationId == orgB)).Should().Be(0,
            "the import must not leak into another organisation");
    }
}
