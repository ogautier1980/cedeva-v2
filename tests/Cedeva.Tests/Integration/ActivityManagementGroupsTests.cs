using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage for the enriched Groups/PrintGroups screens (Lot C): multi-group selection,
/// the Prevus/Present/Signature printable columns, "print all of today's groups", and the
/// PDF/Excel export actions.
/// </summary>
[Collection("WebApp")]
public class ActivityManagementGroupsTests
{
    private static ActivityDay Day(Activity activity, string label, DateTime date) => new()
    {
        Label = label,
        DayDate = date,
        IsActive = true,
        Activity = activity
    };

    [Fact]
    public async Task Groups_Get_NoFilters_ListsChildrenFromAllGroups()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Groupes");
            var groupA = TestData.Group(activity, "Groupe Rouge");
            var groupB = TestData.Group(activity, "Groupe Bleu");
            var parentA = TestData.Parent(org);
            var childA = TestData.Child(parentA);
            childA.LastName = "RougeEnfant";
            var bookingA = TestData.Booking(childA, activity, groupA, 100m, 100m);
            bookingA.IsConfirmed = true;

            var parentB = TestData.Parent(org);
            parentB.Email = "paul2@test.be";
            parentB.NationalRegisterNumber = "85010112346";
            var childB = TestData.Child(parentB);
            childB.LastName = "BleuEnfant";
            childB.NationalRegisterNumber = "16052012346";
            var bookingB = TestData.Booking(childB, activity, groupB, 100m, 100m);
            bookingB.IsConfirmed = true;

            ctx.AddRange(org, activity, groupA, groupB, parentA, childA, bookingA, parentB, childB, bookingB);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Groups?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("RougeEnfant");
        html.Should().Contain("BleuEnfant");
    }

    [Fact]
    public async Task Groups_Get_WithGroupIdsFilter_ExcludesOtherGroups()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int groupAId = 0;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Filtre Groupe");
            var groupA = TestData.Group(activity, "Groupe Rouge");
            var groupB = TestData.Group(activity, "Groupe Bleu");
            var parentA = TestData.Parent(org);
            var childA = TestData.Child(parentA);
            childA.LastName = "SeulRougeEnfant";
            var bookingA = TestData.Booking(childA, activity, groupA, 100m, 100m);
            bookingA.IsConfirmed = true;

            var parentB = TestData.Parent(org);
            parentB.Email = "paul3@test.be";
            parentB.NationalRegisterNumber = "85010112347";
            var childB = TestData.Child(parentB);
            childB.LastName = "SeulBleuEnfant";
            childB.NationalRegisterNumber = "16052012347";
            var bookingB = TestData.Booking(childB, activity, groupB, 100m, 100m);
            bookingB.IsConfirmed = true;

            ctx.AddRange(org, activity, groupA, groupB, parentA, childA, bookingA, parentB, childB, bookingB);
            ctx.SaveChanges();
            groupAId = groupA.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Groups?id={activity.Id}&groupIds={groupAId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("SeulRougeEnfant");
        html.Should().NotContain("SeulBleuEnfant");
    }

    [Fact]
    public async Task Groups_Get_ActivityDayMatchesToday_ExposesPrintAllGroupsTodayButton()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Jour");
            var today = Day(activity, "Aujourd'hui", DateTime.Today);
            ctx.AddRange(org, activity, today);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/Groups?id={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("PrintGroups");
    }

    [Fact]
    public async Task PrintGroups_Get_WithSignatureColumn_RendersSignatureHeader()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Signature");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 100m);
            booking.IsConfirmed = true;
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync(
            $"/ActivityManagement/PrintGroups?activityId={activity.Id}&showSignature=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Signature");
    }

    [Fact]
    public async Task PrintGroups_Get_MultipleGroupIds_ListsBothGroupLabelsInHeader()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        int groupAId = 0, groupBId = 0;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Multi Print");
            var groupA = TestData.Group(activity, "Groupe Soleil");
            var groupB = TestData.Group(activity, "Groupe Lune");
            ctx.AddRange(org, activity, groupA, groupB);
            ctx.SaveChanges();
            groupAId = groupA.Id;
            groupBId = groupB.Id;
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync(
            $"/ActivityManagement/PrintGroups?activityId={activity.Id}&groupIds={groupAId}&groupIds={groupBId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Groupe Soleil");
        html.Should().Contain("Groupe Lune");
    }

    [Fact]
    public async Task ExportGroupsExcel_ReturnsXlsxFile()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Export Excel");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 100m);
            booking.IsConfirmed = true;
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/ExportGroupsExcel?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await response.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportGroupsPdf_ReturnsPdfFile()
    {
        using var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Export Pdf");
            var parent = TestData.Parent(org);
            var child = TestData.Child(parent);
            var booking = TestData.Booking(child, activity, null, 100m, 100m);
            booking.IsConfirmed = true;
            ctx.AddRange(org, activity, parent, child, booking);
            return 0;
        });

        var client = factory.CreateClientFor("u1", org.Id, "Coordinator");
        var response = await client.GetAsync($"/ActivityManagement/ExportGroupsPdf?activityId={activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await response.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportGroupsExcel_WithoutActivity_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        factory.Seed(_ => 0);
        var client = factory.CreateClientFor("u1", 1, "Coordinator");

        var response = await client.GetAsync("/ActivityManagement/ExportGroupsExcel?activityId=999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
