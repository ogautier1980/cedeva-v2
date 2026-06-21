using System.Text;
using Cedeva.Infrastructure.Services.Import;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Services;

/// <summary>
/// Coverage for <see cref="ParentCsvImporter"/> over a real EF schema (SQLite): happy path, parent
/// de-duplication by email, child de-duplication by NRN, and per-row validation.
/// </summary>
public class ParentImportServiceTests
{
    private const string Header =
        "ParentFirstName;ParentLastName;Email;MobilePhone;ParentNRN;Street;PostalCode;City;ChildFirstName;ChildLastName;ChildBirthDate;ChildNRN";

    private static (SqliteTestContext Db, int OrgId) NewDb()
    {
        var db = new SqliteTestContext();
        var org = TestData.Organisation("Org Import");
        using var seed = db.NewContext(FakeCurrentUserService.Admin());
        seed.Add(org);
        seed.SaveChanges();
        return (db, org.Id);
    }

    private static Stream Csv(params string[] lines) =>
        new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

    [Fact]
    public async Task Import_ValidRows_CreatesParentsChildrenAndAddress()
    {
        var (db, orgId) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgId));
        var sut = new ParentCsvImporter(ctx);

        var result = await sut.ImportAsync(Csv(
            Header,
            "Marc;Dupont;marc@test.be;0470000000;;Rue Haute 1;1000;Bruxelles;Léa;Dupont;15/06/2018;",
            "Sophie;Martin;sophie@test.be;0470000001;;Av. Louise 2;1050;Ixelles;Tom;Martin;01/09/2019;"), orgId);

        result.Created.Should().Be(2);
        result.Extra.GetValueOrDefault("Import.ChildrenCreated").Should().Be(2);
        result.HasErrors.Should().BeFalse();

        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        (await verify.Parents.IgnoreQueryFilters().CountAsync(p => p.OrganisationId == orgId)).Should().Be(2);
        (await verify.Children.IgnoreQueryFilters().CountAsync()).Should().Be(2);
        var marc = await verify.Parents.IgnoreQueryFilters().Include(p => p.Address).FirstAsync(p => p.Email == "marc@test.be");
        marc.Address.City.Should().Be("Bruxelles");
    }

    [Fact]
    public async Task Import_SameEmailTwice_ReusesParent()
    {
        var (db, orgId) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgId));
        var sut = new ParentCsvImporter(ctx);

        var result = await sut.ImportAsync(Csv(
            Header,
            "Marc;Dupont;marc@test.be;0470000000;;Rue Haute 1;1000;Bruxelles;Léa;Dupont;15/06/2018;",
            "Marc;Dupont;marc@test.be;0470000000;;Rue Haute 1;1000;Bruxelles;Nina;Dupont;03/03/2020;"), orgId);

        result.Created.Should().Be(1);
        result.Extra.GetValueOrDefault("Import.ParentsReused").Should().Be(1);
        result.Extra.GetValueOrDefault("Import.ChildrenCreated").Should().Be(2);

        await using var verify = db.NewContext(FakeCurrentUserService.Admin());
        (await verify.Parents.IgnoreQueryFilters().CountAsync(p => p.Email == "marc@test.be")).Should().Be(1);
    }

    [Fact]
    public async Task Import_InvalidDate_RecordsRowErrorAndSkips()
    {
        var (db, orgId) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgId));
        var sut = new ParentCsvImporter(ctx);

        var result = await sut.ImportAsync(Csv(
            Header,
            "Marc;Dupont;marc@test.be;0470000000;;Rue Haute 1;1000;Bruxelles;Léa;Dupont;not-a-date;"), orgId);

        result.Extra.GetValueOrDefault("Import.ChildrenCreated").Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().Contain("Ligne 2");
    }

    [Fact]
    public async Task Import_MissingRequiredField_RecordsRowError()
    {
        var (db, orgId) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgId));
        var sut = new ParentCsvImporter(ctx);

        var result = await sut.ImportAsync(Csv(
            Header,
            ";Dupont;marc@test.be;0470000000;;Rue Haute 1;1000;Bruxelles;Léa;Dupont;15/06/2018;"), orgId);

        result.Created.Should().Be(0);
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task Import_MissingHeaderColumn_FailsWithoutImporting()
    {
        var (db, orgId) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgId));
        var sut = new ParentCsvImporter(ctx);

        var result = await sut.ImportAsync(Csv(
            "ParentFirstName;ParentLastName;MobilePhone;Street;PostalCode;City;ChildFirstName;ChildLastName;ChildBirthDate",
            "Marc;Dupont;0470000000;Rue Haute 1;1000;Bruxelles;Léa;Dupont;15/06/2018"), orgId);

        result.Created.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().Contain("email");
    }

    [Fact]
    public async Task Import_DuplicateChildNrn_SkipsSecondChild()
    {
        var (db, orgId) = NewDb();
        using var _d = db;
        using var ctx = db.NewContext(FakeCurrentUserService.Coordinator(orgId));
        var sut = new ParentCsvImporter(ctx);

        // 95061512309 is a valid Belgian NRN (mod-97 check passes).
        var result = await sut.ImportAsync(Csv(
            Header,
            "Marc;Dupont;marc@test.be;0470000000;;Rue Haute 1;1000;Bruxelles;Léa;Dupont;15/06/2018;95.06.15-123.09",
            "Marc;Dupont;marc@test.be;0470000000;;Rue Haute 1;1000;Bruxelles;Léa;Dupont;15/06/2018;95.06.15-123.09"), orgId);

        result.Extra.GetValueOrDefault("Import.ChildrenCreated").Should().Be(1);
        result.Extra.GetValueOrDefault("Import.ChildrenSkipped").Should().Be(1);
    }
}
