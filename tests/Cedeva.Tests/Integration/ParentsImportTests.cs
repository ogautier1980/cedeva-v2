using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>End-to-end coverage of the parents CSV import endpoint (multipart upload -> result view).</summary>
[Collection("WebApp")]
public class ParentsImportTests
{
    [Fact]
    public async Task Import_Post_WithCsvFile_ImportsAndShowsResult()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx => { org = TestData.Organisation(); ctx.Add(org); return 0; });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        const string csv =
            "ParentFirstName;ParentLastName;Email;MobilePhone;ParentNRN;Street;PostalCode;City;ChildFirstName;ChildLastName;ChildBirthDate;ChildNRN\n" +
            "Marc;Dupont;marc@test.be;0470000000;;Rue Haute 1;1000;Bruxelles;Léa;Dupont;15/06/2018;";

        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "csvFile", "import.csv");

        var response = await client.PostAsync("/Parents/Import", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the result view is rendered");

        using var db = factory.NewDbContext();
        (await db.Parents.IgnoreQueryFilters().AnyAsync(p => p.Email == "marc@test.be")).Should().BeTrue();
        (await db.Children.IgnoreQueryFilters().AnyAsync(c => c.FirstName == "Léa")).Should().BeTrue();
    }

    [Fact]
    public async Task Import_Post_WithNonCsvFile_ReturnsViewWithError_AndImportsNothing()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx => { org = TestData.Organisation(); ctx.Add(org); return 0; });
        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("not a csv"));
        content.Add(file, "csvFile", "data.txt");

        var response = await client.PostAsync("/Parents/Import", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var db = factory.NewDbContext();
        (await db.Parents.IgnoreQueryFilters().AnyAsync(p => p.OrganisationId == org.Id)).Should().BeFalse();
    }
}
