using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Services.Activities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Services.Activities;

public class ExcursionViewModelBuilderServiceTests
{
    // Simple deterministic localizer so we can assert the mapped value.
    private static string Localize(PaymentStatus status) => $"PS:{status}";

    private static ExcursionViewModelBuilderService BuildSut(SqliteTestContext db) =>
        new(db.Context, new ExcursionService(db.Context));

    // ---------- BuildRegistrationsByGroupAsync ----------

    [Fact]
    public async Task BuildRegistrationsByGroup_UnknownExcursion_ReturnsEmptyDictionary()
    {
        using var db = new SqliteTestContext();
        var sut = BuildSut(db);

        var result = await sut.BuildRegistrationsByGroupAsync(99999, Localize);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildRegistrationsByGroup_GroupsConfirmedBookingsByEligibleGroup_AndMapsFields()
    {
        using var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var group = TestData.Group(activity, "Lions");
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);
        child.FirstName = "Anna";
        child.LastName = "Zola";

        var booking = TestData.Booking(child, activity, group, totalAmount: 100m, paidAmount: 0m);
        booking.PaymentStatus = PaymentStatus.PartiallyPaid;

        var excursion = TestData.Excursion(activity, cost: 12m);
        var link = TestData.ExcursionGroup(excursion, group);

        ctx.AddRange(org, activity, group, parent, child, booking, excursion, link);
        ctx.SaveChanges();

        var sut = BuildSut(db);

        var result = await sut.BuildRegistrationsByGroupAsync(excursion.Id, Localize);

        result.Should().HaveCount(1);
        var entry = result.Single();
        entry.Key.Label.Should().Be("Lions");
        entry.Value.Should().HaveCount(1);

        var info = entry.Value.Single();
        info.BookingId.Should().Be(booking.Id);
        info.ChildId.Should().Be(child.Id);
        info.FirstName.Should().Be("Anna");
        info.LastName.Should().Be("Zola");
        info.BirthDate.Should().Be(child.BirthDate);
        info.ExcursionCost.Should().Be(12m);
        info.PaymentStatus.Should().Be("PS:PartiallyPaid");
        info.IsRegistered.Should().BeFalse();
        info.RegistrationId.Should().BeNull();
    }

    [Fact]
    public async Task BuildRegistrationsByGroup_RegisteredChild_MarksRegisteredWithRegistrationId()
    {
        using var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var group = TestData.Group(activity, "Lions");
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);
        var booking = TestData.Booking(child, activity, group, totalAmount: 100m, paidAmount: 0m);
        var excursion = TestData.Excursion(activity, cost: 10m);
        var link = TestData.ExcursionGroup(excursion, group);

        ctx.AddRange(org, activity, group, parent, child, booking, excursion, link);
        ctx.SaveChanges();

        // Register through the real service so the registration row is consistent.
        var registration = await new ExcursionService(db.Context)
            .RegisterChildAsync(excursion.Id, booking.Id);

        var sut = BuildSut(db);
        var result = await sut.BuildRegistrationsByGroupAsync(excursion.Id, Localize);

        var info = result.Single().Value.Single();
        info.IsRegistered.Should().BeTrue();
        info.RegistrationId.Should().Be(registration.Id);
    }

    [Fact]
    public async Task BuildRegistrationsByGroup_ExcludesUnconfirmedBookings()
    {
        using var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var group = TestData.Group(activity, "Lions");
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);
        var booking = TestData.Booking(child, activity, group, 100m, 0m);
        booking.IsConfirmed = false;
        var excursion = TestData.Excursion(activity, cost: 10m);
        var link = TestData.ExcursionGroup(excursion, group);

        ctx.AddRange(org, activity, group, parent, child, booking, excursion, link);
        ctx.SaveChanges();

        var sut = BuildSut(db);
        var result = await sut.BuildRegistrationsByGroupAsync(excursion.Id, Localize);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildRegistrationsByGroup_ExcludesBookingsInIneligibleGroups()
    {
        using var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var eligible = TestData.Group(activity, "Lions");
        var ineligible = TestData.Group(activity, "Tigres");
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);
        // Booking belongs to a group that is NOT linked to the excursion.
        var booking = TestData.Booking(child, activity, ineligible, 100m, 0m);
        var excursion = TestData.Excursion(activity, cost: 10m);
        var link = TestData.ExcursionGroup(excursion, eligible);

        ctx.AddRange(org, activity, eligible, ineligible, parent, child, booking, excursion, link);
        ctx.SaveChanges();

        var sut = BuildSut(db);
        var result = await sut.BuildRegistrationsByGroupAsync(excursion.Id, Localize);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildRegistrationsByGroup_ExcludesBookingsWithoutGroup()
    {
        using var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var group = TestData.Group(activity, "Lions");
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);
        var booking = TestData.Booking(child, activity, group: null, totalAmount: 100m, paidAmount: 0m);
        var excursion = TestData.Excursion(activity, cost: 10m);
        var link = TestData.ExcursionGroup(excursion, group);

        ctx.AddRange(org, activity, group, parent, child, booking, excursion, link);
        ctx.SaveChanges();

        var sut = BuildSut(db);
        var result = await sut.BuildRegistrationsByGroupAsync(excursion.Id, Localize);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildRegistrationsByGroup_SortsChildrenByLastNameThenFirstName()
    {
        using var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var group = TestData.Group(activity, "Lions");
        var parent = TestData.Parent(org);

        var childB = TestData.Child(parent);
        childB.FirstName = "Marc";
        childB.LastName = "Bernard";

        var childA2 = TestData.Child(parent);
        childA2.FirstName = "Zoe";
        childA2.LastName = "Albert";

        var childA1 = TestData.Child(parent);
        childA1.FirstName = "Alice";
        childA1.LastName = "Albert";

        var bB = TestData.Booking(childB, activity, group, 100m, 0m);
        var bA2 = TestData.Booking(childA2, activity, group, 100m, 0m);
        var bA1 = TestData.Booking(childA1, activity, group, 100m, 0m);

        var excursion = TestData.Excursion(activity, cost: 10m);
        var link = TestData.ExcursionGroup(excursion, group);

        ctx.AddRange(org, activity, group, parent, childB, childA2, childA1, bB, bA2, bA1, excursion, link);
        ctx.SaveChanges();

        var sut = BuildSut(db);
        var result = await sut.BuildRegistrationsByGroupAsync(excursion.Id, Localize);

        var ordered = result.Single().Value;
        ordered.Select(c => $"{c.LastName} {c.FirstName}")
            .Should().ContainInOrder("Albert Alice", "Albert Zoe", "Bernard Marc");
    }

    // ---------- BuildAttendanceByGroupAsync ----------

    [Fact]
    public async Task BuildAttendanceByGroup_NoRegistrations_ReturnsEmptyDictionary()
    {
        using var db = new SqliteTestContext();
        var sut = BuildSut(db);

        var result = await sut.BuildAttendanceByGroupAsync(99999);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAttendanceByGroup_GroupsRegistrationsByGroup_AndMapsFields()
    {
        using var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var group = TestData.Group(activity, "Lions");
        var parent = TestData.Parent(org);
        var child = TestData.Child(parent);
        child.FirstName = "Tom";
        child.LastName = "Durand";
        var booking = TestData.Booking(child, activity, group, 100m, 0m);
        var excursion = TestData.Excursion(activity, cost: 10m);
        var link = TestData.ExcursionGroup(excursion, group);

        ctx.AddRange(org, activity, group, parent, child, booking, excursion, link);
        ctx.SaveChanges();

        var registration = await new ExcursionService(db.Context)
            .RegisterChildAsync(excursion.Id, booking.Id);
        await new ExcursionService(db.Context).UpdateAttendanceAsync(registration.Id, true);

        var sut = BuildSut(db);
        var result = await sut.BuildAttendanceByGroupAsync(excursion.Id);

        result.Should().HaveCount(1);
        var entry = result.Single();
        entry.Key.Label.Should().Be("Lions");

        var info = entry.Value.Single();
        info.RegistrationId.Should().Be(registration.Id);
        info.BookingId.Should().Be(booking.Id);
        info.FirstName.Should().Be("Tom");
        info.LastName.Should().Be("Durand");
        info.BirthDate.Should().Be(child.BirthDate);
        info.IsPresent.Should().BeTrue();
    }

    [Fact]
    public async Task BuildAttendanceByGroup_SortsChildrenByLastNameThenFirstName()
    {
        using var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var group = TestData.Group(activity, "Lions");
        var parent = TestData.Parent(org);

        var childB = TestData.Child(parent);
        childB.FirstName = "Marc";
        childB.LastName = "Bernard";
        var childA = TestData.Child(parent);
        childA.FirstName = "Alice";
        childA.LastName = "Albert";

        var bB = TestData.Booking(childB, activity, group, 100m, 0m);
        var bA = TestData.Booking(childA, activity, group, 100m, 0m);

        var excursion = TestData.Excursion(activity, cost: 10m);
        var link = TestData.ExcursionGroup(excursion, group);

        ctx.AddRange(org, activity, group, parent, childB, childA, bB, bA, excursion, link);
        ctx.SaveChanges();

        var service = new ExcursionService(db.Context);
        await service.RegisterChildAsync(excursion.Id, bB.Id);
        await service.RegisterChildAsync(excursion.Id, bA.Id);

        var sut = BuildSut(db);
        var result = await sut.BuildAttendanceByGroupAsync(excursion.Id);

        var ordered = result.Single().Value;
        ordered.Select(c => c.LastName).Should().ContainInOrder("Albert", "Bernard");
    }
}
