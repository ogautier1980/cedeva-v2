using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Services.Email;
using Cedeva.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cedeva.Tests.Services.Email;

public class EmailRecipientServiceTests
{
    private sealed record Scenario(SqliteTestContext Db, int ActivityId, int GroupG1Id, int DayId);

    // Seeds confirmed bookings with distinct parents, medical-sheet flags, groups and a reserved
    // day, plus one unconfirmed booking (must always be excluded).
    private static Scenario Seed()
    {
        var db = new SqliteTestContext();
        var ctx = db.Context;

        var org = TestData.Organisation();
        var activity = TestData.Activity(org);
        var day1 = new ActivityDay { Label = "J1", DayDate = new DateTime(2026, 7, 6), IsActive = true };
        activity.Days.Add(day1);
        var g1 = TestData.Group(activity, "G1");
        var g2 = TestData.Group(activity, "G2");

        // Parent P: confirmed, group G1, HAS medical sheet, reserved on day1
        var p = TestData.Parent(org); p.Email = "p@x.be";
        var bP = TestData.Booking(TestData.Child(p), activity, g1, 100m, 0m);
        bP.IsMedicalSheet = true;
        bP.Days.Add(new BookingDay { ActivityDay = day1, IsReserved = true });

        // Parent Q: confirmed, group G2, no medical sheet, day1 NOT reserved
        var q = TestData.Parent(org); q.Email = "q@x.be";
        var bQ = TestData.Booking(TestData.Child(q), activity, g2, 100m, 0m);
        bQ.IsMedicalSheet = false;
        bQ.Days.Add(new BookingDay { ActivityDay = day1, IsReserved = false });

        // Parent R: two confirmed bookings (group G1) to exercise Distinct; one reserved on day1
        var r = TestData.Parent(org); r.Email = "r@x.be";
        var bR1 = TestData.Booking(TestData.Child(r), activity, g1, 100m, 0m);
        bR1.IsMedicalSheet = false;
        bR1.Days.Add(new BookingDay { ActivityDay = day1, IsReserved = true });
        var bR2 = TestData.Booking(TestData.Child(r), activity, g1, 100m, 0m);
        bR2.IsMedicalSheet = false; // no day for day1

        // Parent S: UNCONFIRMED booking -> always excluded
        var s = TestData.Parent(org); s.Email = "s@x.be";
        var bS = TestData.Booking(TestData.Child(s), activity, g1, 100m, 0m);
        bS.IsConfirmed = false;

        ctx.AddRange(org, activity, day1, g1, g2,
            p, bP, q, bQ, r, bR1, bR2, s, bS);
        ctx.SaveChanges();

        return new Scenario(db, activity.Id, g1.Id, day1.DayId);
    }

    private static EmailRecipientService Sut(SqliteTestContext db) =>
        new(db.Context, NullLogger<EmailRecipientService>.Instance);

    [Fact]
    public async Task AllParents_ReturnsDistinctConfirmedParentEmails()
    {
        var s = Seed();
        using var _ = s.Db;

        var emails = await Sut(s.Db).GetRecipientEmailsAsync(s.ActivityId, "allparents");

        emails.Should().BeEquivalentTo(new[] { "p@x.be", "q@x.be", "r@x.be" }); // S unconfirmed; R once
    }

    [Fact]
    public async Task MedicalSheetReminder_ExcludesBookingsThatHaveTheSheet()
    {
        var s = Seed();
        using var _ = s.Db;

        var emails = await Sut(s.Db).GetRecipientEmailsAsync(s.ActivityId, "medicalsheetreminder");

        emails.Should().BeEquivalentTo(new[] { "q@x.be", "r@x.be" }); // P has the sheet -> excluded
    }

    [Fact]
    public async Task GroupFilter_ReturnsOnlyParentsInThatGroup()
    {
        var s = Seed();
        using var _ = s.Db;

        var emails = await Sut(s.Db).GetRecipientEmailsAsync(s.ActivityId, "group_x", recipientGroupId: s.GroupG1Id);

        emails.Should().BeEquivalentTo(new[] { "p@x.be", "r@x.be" }); // Q is in G2 -> excluded
    }

    [Fact]
    public async Task DayFilter_ReturnsOnlyParentsReservedOnThatDay()
    {
        var s = Seed();
        using var _ = s.Db;

        var emails = await Sut(s.Db).GetRecipientEmailsAsync(s.ActivityId, "allparents", scheduledDayId: s.DayId);

        emails.Should().BeEquivalentTo(new[] { "p@x.be", "r@x.be" }); // Q not reserved; R2 has no day
    }
}
