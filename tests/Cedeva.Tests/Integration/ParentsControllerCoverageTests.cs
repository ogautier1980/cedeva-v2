using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Additional ParentsController integration coverage. Deliberately exercises the actions and
/// branches NOT touched by <see cref="ParentsControllerIntegrationTests"/>:
/// Index search/sort + clean-URL redirect, Export (Excel) + ExportPdf, Edit GET, Delete GET,
/// CreateAjax (JSON), admin organisation handling and tenant isolation on those paths.
/// </summary>
[Collection("WebApp")]
public class ParentsControllerCoverageTests
{
    private const string ValidParentNrnFormatted = "85.06.15-133.80";
    private const string ValidParentNrnStripped = "85061513380";

    private static Dictionary<string, string> ValidParentForm() => new()
    {
        ["FirstName"] = "Jean",
        ["LastName"] = "Dupont",
        ["Email"] = "jean.dupont@test.be",
        ["MobilePhoneNumber"] = "0470123456",
        ["NationalRegisterNumber"] = ValidParentNrnFormatted,
        ["Street"] = "Rue du Test 1",
        ["City"] = "Bruxelles",
        ["PostalCode"] = "1000",
        ["Country"] = "Belgium"
    };

    // ----------------------------------------------------------------------------
    // GET Index — search + sort + clean-URL redirect
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task Index_WithQueryParams_RedirectsToCleanUrl()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Parents?SearchString=Dupont");

        // Index stores filters in session then redirects to the clean URL "/Parents".
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/Parents");
    }

    [Fact]
    public async Task Index_SearchString_FiltersByName()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();

            var keep = TestData.Parent(org);
            keep.LastName = "Findable";
            keep.Email = "findable@test.be";

            var drop = TestData.Parent(org);
            drop.LastName = "Hiddenname";
            drop.Email = "hidden@test.be";

            ctx.AddRange(org, keep, drop);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        // First request stores the search filter in session and redirects.
        var redirect = await client.GetAsync("/Parents?SearchString=Findable");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        // Following the clean URL (same session cookie) applies the stored filter.
        var listed = await client.GetAsync("/Parents");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await listed.Content.ReadAsStringAsync();
        html.Should().Contain("Findable");
        html.Should().NotContain("Hiddenname");
    }

    [Fact]
    public async Task Index_SortByFirstNameDescending_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();

            var a = TestData.Parent(org);
            a.FirstName = "Aaron";
            a.Email = "aaron@test.be";

            var z = TestData.Parent(org);
            z.FirstName = "Zoe";
            z.Email = "zoe@test.be";

            ctx.AddRange(org, a, z);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var redirect = await client.GetAsync("/Parents?SortBy=firstname&SortOrder=desc");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var listed = await client.GetAsync("/Parents");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await listed.Content.ReadAsStringAsync();
        html.Should().Contain("Aaron");
        html.Should().Contain("Zoe");
    }

    [Fact]
    public async Task Index_SortByCityAscending_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");

        var redirect = await client.GetAsync("/Parents?SortBy=city&SortOrder=asc");
        redirect.StatusCode.Should().Be(HttpStatusCode.Found);

        var listed = await client.GetAsync("/Parents");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_AsAdmin_SeesAllOrganisationsParents()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            var orgA = TestData.Organisation("Org A");
            var orgB = TestData.Organisation("Org B");

            var pA = TestData.Parent(orgA);
            pA.LastName = "AdminAlpha";

            var pB = TestData.Parent(orgB);
            pB.LastName = "AdminBravo";
            pB.Email = "bravo@test.be";

            ctx.AddRange(orgA, orgB, pA, pB);
            return 0;
        });

        // Admin bypasses the tenancy filter and sees parents from every organisation.
        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.GetAsync("/Parents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("AdminAlpha");
        html.Should().Contain("AdminBravo");
    }

    // ----------------------------------------------------------------------------
    // GET Export (Excel)
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task Export_ReturnsNonEmptyExcelFile()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Parents/Export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_WithSearchTerm_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            parent.LastName = "Exportable";
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Parents/Export?searchTerm=Exportable");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    // ----------------------------------------------------------------------------
    // GET ExportPdf
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task ExportPdf_ReturnsNonEmptyPdfFile()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync("/Parents/ExportPdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    // ----------------------------------------------------------------------------
    // GET Details — with children listed
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task Details_ParentWithChildren_RendersChildNames()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            child.FirstName = "Léa";
            child.LastName = "Junior";
            ctx.AddRange(org, parent, child);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Parents/Details/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Junior");
    }

    // ----------------------------------------------------------------------------
    // GET Edit
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task EditForm_ExistingParent_ReturnsOkWithData()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            parent.LastName = "EditableParent";
            parent.NationalRegisterNumber = ValidParentNrnStripped;
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Parents/Edit/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("EditableParent");
        // NRN is reformatted for display (YY.MM.DD-XXX.XX).
        html.Should().Contain(ValidParentNrnFormatted);
    }

    [Fact]
    public async Task EditForm_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", 1, "Coordinator");
        var response = await client.GetAsync("/Parents/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditForm_ParentInAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/Parents/Edit/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------------------------
    // POST Edit — return url + tenant isolation
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task EditPost_ParentInAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });

        var form = ValidParentForm();
        form["Id"] = parent.Id.ToString();
        form["FirstName"] = "Hacker";

        // Other-org coordinator: id matches route but the tenancy filter hides the row,
        // so the post-validation lookup returns null => NotFound.
        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.PostAsync($"/Parents/Edit/{parent.Id}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var db = factory.NewDbContext();
        var unchanged = await db.Parents.IgnoreQueryFilters().FirstAsync(p => p.Id == parent.Id);
        unchanged.FirstName.Should().NotBe("Hacker");
    }

    [Fact]
    public async Task EditPost_WithReturnUrl_RedirectsToReturnUrl()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            parent.NationalRegisterNumber = ValidParentNrnStripped;
            ctx.AddRange(org, parent);
            return 0;
        });

        var form = ValidParentForm();
        form["Id"] = parent.Id.ToString();
        form["FirstName"] = "Retour";

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync(
            $"/Parents/Edit/{parent.Id}?returnUrl=/Children/Details/5",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("/Children/Details/5");

        using var db = factory.NewDbContext();
        var updated = await db.Parents.IgnoreQueryFilters().FirstAsync(p => p.Id == parent.Id);
        updated.FirstName.Should().Be("Retour");
    }

    // ----------------------------------------------------------------------------
    // GET Delete (confirmation page)
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task DeleteForm_ExistingParent_ReturnsOk()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            parent = TestData.Parent(org);
            parent.LastName = "DeletableParent";
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/Parents/Delete/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("DeletableParent");
    }

    [Fact]
    public async Task DeleteForm_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClientFor("u1", 1, "Coordinator");
        var response = await client.GetAsync("/Parents/Delete/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteForm_ParentInAnotherOrganisation_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            parent = TestData.Parent(org);
            ctx.AddRange(org, parent);
            return 0;
        });

        var client = factory.CreateClientFor("u1", organisationId: 99999, "Coordinator");
        var response = await client.GetAsync($"/Parents/Delete/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------------------------
    // POST CreateAjax (JSON)
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task CreateAjax_ValidModel_ReturnsSuccessJsonAndPersists()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync("/Parents/CreateAjax",
            new FormUrlEncodedContent(ValidParentForm()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("parentId").GetInt32().Should().BeGreaterThan(0);

        using var db = factory.NewDbContext();
        var persisted = await db.Parents.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.LastName == "Dupont");
        persisted.Should().NotBeNull();
        persisted!.OrganisationId.Should().Be(org.Id);
        persisted.NationalRegisterNumber.Should().Be(ValidParentNrnStripped);
    }

    [Fact]
    public async Task CreateAjax_InvalidModel_ReturnsFailureJsonWithErrors()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            ctx.Add(org);
            return 0;
        });

        var form = ValidParentForm();
        form["FirstName"] = "";
        form["Email"] = "";

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.PostAsync("/Parents/CreateAjax",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.TryGetProperty("errors", out _).Should().BeTrue();

        using var db = factory.NewDbContext();
        (await db.Parents.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // ----------------------------------------------------------------------------
    // Admin organisation handling
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task CreatePost_AsAdmin_UsesViewModelOrganisationId()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation orgA = null!;
        Organisation orgB = null!;
        factory.Seed(ctx =>
        {
            orgA = TestData.Organisation("Org A");
            orgB = TestData.Organisation("Org B");
            ctx.AddRange(orgA, orgB);
            return 0;
        });

        var form = ValidParentForm();
        form["LastName"] = "AdminCreated";
        form["OrganisationId"] = orgB.Id.ToString();

        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.PostAsync("/Parents/Create", new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var persisted = await db.Parents.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.LastName == "AdminCreated");
        persisted.Should().NotBeNull();
        // Admin path honours the OrganisationId chosen in the form, not the (null) session org.
        persisted!.OrganisationId.Should().Be(orgB.Id);
    }

    [Fact]
    public async Task EditPost_AsAdmin_UpdatesOrganisationId()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation orgA = null!;
        Organisation orgB = null!;
        Parent parent = null!;
        factory.Seed(ctx =>
        {
            orgA = TestData.Organisation("Org A");
            orgB = TestData.Organisation("Org B");
            parent = TestData.Parent(orgA);
            parent.NationalRegisterNumber = ValidParentNrnStripped;
            ctx.AddRange(orgA, orgB, parent);
            return 0;
        });

        var form = ValidParentForm();
        form["Id"] = parent.Id.ToString();
        form["OrganisationId"] = orgB.Id.ToString();

        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.PostAsync($"/Parents/Edit/{parent.Id}",
            new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var db = factory.NewDbContext();
        var updated = await db.Parents.IgnoreQueryFilters().FirstAsync(p => p.Id == parent.Id);
        updated.OrganisationId.Should().Be(orgB.Id);
    }

    [Fact]
    public async Task CreateForm_AsAdmin_PopulatesOrganisationDropdown()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(ctx =>
        {
            ctx.AddRange(TestData.Organisation("Alpha Org"), TestData.Organisation("Beta Org"));
            return 0;
        });

        var client = factory.CreateClientFor("admin", organisationId: null, "Admin");
        var response = await client.GetAsync("/Parents/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Alpha Org");
        html.Should().Contain("Beta Org");
    }

    // ----------------------------------------------------------------------------
    // Authentication on additional endpoints
    // ----------------------------------------------------------------------------

    [Fact]
    public async Task Export_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Parents/Export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAjax_WithoutAuth_IsChallenged()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/Parents/CreateAjax",
            new FormUrlEncodedContent(ValidParentForm()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
