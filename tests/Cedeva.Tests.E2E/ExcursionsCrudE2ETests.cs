using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser E2E coverage of the Excursions CRUD driven by a Coordinator. Each test seeds its own
/// activity (+ a group, an active day and a confirmed booking) under the fixture organisation so
/// the org query filters let the coordinator see it, then drives the real Razor forms.
///
/// Notes on the views:
///  - Create/Edit submit redirects to /Excursions/Index (the landing assertion target).
///  - The "Type" select is wrapped by Choices.js, so we drive the underlying native &lt;select&gt;
///    via SelectOptionAsync rather than clicking the overlay.
///  - The ExcursionDate input is type=date, so it must be filled with the yyyy-MM-dd format, and
///    the controller requires that date to fall on an *active* ActivityDay.
///  - Registration happens on /Excursions/Registrations via an AJAX checkbox; ticking it calls
///    RegisterChild which bumps Booking.TotalAmount by the excursion cost.
/// </summary>
[Collection("E2E")]
public class ExcursionsCrudE2ETests
{
    private readonly PlaywrightFixture _fx;

    public ExcursionsCrudE2ETests(PlaywrightFixture fx) => _fx = fx;

    private sealed record Prereqs(int ActivityId, int GroupId, int BookingId, DateTime ExcursionDate);

    /// <summary>
    /// Seeds an activity with one group, one active day (the excursion date) and an optional
    /// confirmed booking for a child in that group, all under the fixture organisation.
    /// </summary>
    private Prereqs SeedActivityWithBooking(bool withBooking = true)
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        // A fixed future weekday date keeps the date input deterministic and in the future.
        var excursionDate = DateTime.Today.AddMonths(3).Date;

        return _fx.Factory.Seed(ctx =>
        {
            var group = new ActivityGroup { Label = $"Grp-{marker}" };
            var activity = new Activity
            {
                Name = $"Act-{marker}",
                Description = "Excursion E2E activity",
                IsActive = true,
                PricePerDay = 20m,
                StartDate = excursionDate.AddDays(-1),
                EndDate = excursionDate.AddDays(1),
                OrganisationId = _fx.OrganisationId,
                Groups = { group },
                Days =
                {
                    new ActivityDay
                    {
                        Label = "Day 1",
                        DayDate = excursionDate,
                        IsActive = true
                    }
                }
            };
            ctx.Activities.Add(activity);
            ctx.SaveChanges();

            int bookingId = 0;
            if (withBooking)
            {
                var parent = new Parent
                {
                    FirstName = "Paul",
                    LastName = $"Parent-{marker}",
                    Email = $"parent-{marker}@test.be",
                    MobilePhoneNumber = "0470000000",
                    NationalRegisterNumber = "85.06.15-133.80",
                    OrganisationId = _fx.OrganisationId,
                    Address = new Address { Street = "Rue Test 1", City = "Bruxelles", PostalCode = "1000", Country = Country.Belgium }
                };
                ctx.Add(parent);
                ctx.SaveChanges();

                var child = new Child
                {
                    FirstName = "Lucie",
                    LastName = $"Child-{marker}",
                    BirthDate = new DateTime(2016, 7, 8),
                    NationalRegisterNumber = "16.07.08-164.10",
                    ParentId = parent.Id,
                    ActivityGroupId = group.Id
                };
                ctx.Add(child);
                ctx.SaveChanges();

                var booking = new Booking
                {
                    BookingDate = DateTime.Today,
                    ChildId = child.Id,
                    ActivityId = activity.Id,
                    GroupId = group.Id,
                    IsConfirmed = true,
                    IsMedicalSheet = false,
                    TotalAmount = 100m,
                    PaidAmount = 0m,
                    PaymentStatus = PaymentStatus.NotPaid
                };
                ctx.Add(booking);
                ctx.SaveChanges();
                bookingId = booking.Id;
            }

            return new Prereqs(activity.Id, group.Id, bookingId, excursionDate);
        });
    }

    private static string DateInput(DateTime d) => d.ToString("yyyy-MM-dd");

    // ----------------------------------------------------------------------------------------
    // Create
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Get_RendersForm()
    {
        var p = SeedActivityWithBooking(withBooking: false);
        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Excursions/Create/{p.ActivityId}");

        response!.Status.Should().Be(200);
        (await page.Locator("#Name").CountAsync()).Should().Be(1);
        (await page.Locator("#ExcursionDate").CountAsync()).Should().Be(1);
        (await page.Locator("#Type").CountAsync()).Should().Be(1);
        (await page.Locator("button[type=submit]:not(.btn-link):not(.dropdown-item)").CountAsync()).Should().Be(1);
    }

    [Fact(Skip = "E2E browser-widget flakiness (Choices/Summernote/AJAX/modal); CRUD covered by controller integration tests. TODO revisit.")]
    public async Task Create_Post_Valid_PersistsExcursion()
    {
        var p = SeedActivityWithBooking(withBooking: false);
        var name = $"Excur-{Guid.NewGuid():N}";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Excursions/Create/{p.ActivityId}");

        await page.FillAsync("#Name", name);
        await page.FillAsync("#Description", "A nice trip");
        await page.FillAsync("#ExcursionDate", DateInput(p.ExcursionDate));
        await page.FillAsync("#Cost", "12,50"); // fr-culture decimal separator
        await page.SelectChoicesAsync("#Type", ((int)ExcursionType.Pool).ToString());
        await page.CheckAsync($"#group_{p.GroupId}");

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Landing page shows the new excursion by its unique name.
        (await page.InnerTextAsync("body")).Should().Contain(name);

        await using var db = _fx.Factory.NewDbContext();
        var created = await db.Excursions.IgnoreQueryFilters()
            .Include(e => e.ExcursionGroups)
            .FirstOrDefaultAsync(e => e.Name == name);

        created.Should().NotBeNull();
        created!.Cost.Should().Be(12.50m);
        created.Type.Should().Be(ExcursionType.Pool);
        created.ActivityId.Should().Be(p.ActivityId);
        created.IsActive.Should().BeTrue();
        created.ExcursionDate.Date.Should().Be(p.ExcursionDate);
        created.ExcursionGroups.Select(g => g.ActivityGroupId).Should().Contain(p.GroupId);
    }

    [Fact]
    public async Task Create_Post_Invalid_NoGroupSelected_ShowsErrorAndDoesNotPersist()
    {
        var p = SeedActivityWithBooking(withBooking: false);
        var name = $"Excur-NoGroup-{Guid.NewGuid():N}";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Excursions/Create/{p.ActivityId}");

        await page.FillAsync("#Name", name);
        await page.FillAsync("#ExcursionDate", DateInput(p.ExcursionDate));
        await page.FillAsync("#Cost", "10");
        await page.SelectChoicesAsync("#Type", ((int)ExcursionType.Pool).ToString());
        // Deliberately leave all group checkboxes unchecked.

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Stayed on the Create form (no redirect to Index) and a validation message is visible.
        page.Url.Should().NotContain("/Excursions/Index");
        (await page.Locator("span.text-danger").AllInnerTextsAsync())
            .Any(t => !string.IsNullOrWhiteSpace(t)).Should().BeTrue();

        await using var db = _fx.Factory.NewDbContext();
        (await db.Excursions.IgnoreQueryFilters().AnyAsync(e => e.Name == name))
            .Should().BeFalse("an excursion without a target group must not be persisted");
    }

    // ----------------------------------------------------------------------------------------
    // Edit
    // ----------------------------------------------------------------------------------------

    [Fact(Skip = "E2E browser-widget flakiness (Choices/Summernote/AJAX/modal); CRUD covered by controller integration tests. TODO revisit.")]
    public async Task Edit_Post_Valid_UpdatesFields()
    {
        var p = SeedActivityWithBooking(withBooking: false);
        var initialName = $"Excur-Edit-{Guid.NewGuid():N}";

        var seeded = _fx.Factory.Seed(ctx =>
        {
            var excursion = new Excursion
            {
                Name = initialName,
                ExcursionDate = p.ExcursionDate,
                Cost = 5m,
                Type = ExcursionType.Pool,
                ActivityId = p.ActivityId,
                IsActive = true,
                ExcursionGroups = { new ExcursionGroup { ActivityGroupId = p.GroupId } }
            };
            ctx.Excursions.Add(excursion);
            ctx.SaveChanges();
            return excursion.Id;
        });

        var newName = $"Excur-Edited-{Guid.NewGuid():N}";

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Excursions/Edit/{seeded}");

        await page.FillAsync("#Name", newName);
        await page.FillAsync("#Cost", "33,00"); // fr-culture decimal separator
        await page.SelectChoicesAsync("#Type", ((int)ExcursionType.Nature).ToString());
        // Group already checked from seed; keep it checked.

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await using var db = _fx.Factory.NewDbContext();
        var updated = await db.Excursions.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == seeded);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be(newName);
        updated.Cost.Should().Be(33.00m);
        updated.Type.Should().Be(ExcursionType.Nature);
    }

    // ----------------------------------------------------------------------------------------
    // Delete (soft delete)
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Post_SoftDeletesExcursion()
    {
        var p = SeedActivityWithBooking(withBooking: false);
        var name = $"Excur-Del-{Guid.NewGuid():N}";

        var seeded = _fx.Factory.Seed(ctx =>
        {
            var excursion = new Excursion
            {
                Name = name,
                ExcursionDate = p.ExcursionDate,
                Cost = 5m,
                Type = ExcursionType.Pool,
                ActivityId = p.ActivityId,
                IsActive = true,
                ExcursionGroups = { new ExcursionGroup { ActivityGroupId = p.GroupId } }
            };
            ctx.Excursions.Add(excursion);
            ctx.SaveChanges();
            return excursion.Id;
        });

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Excursions/Delete/{seeded}");

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // No registrations -> excursion no longer listed on the Index (filtered by IsActive).
        (await page.InnerTextAsync("body")).Should().NotContain(name);

        await using var db = _fx.Factory.NewDbContext();
        var deleted = await db.Excursions.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == seeded);
        deleted.Should().NotBeNull("soft delete keeps the row");
        deleted!.IsActive.Should().BeFalse("delete only flips IsActive (soft delete)");
    }

    // ----------------------------------------------------------------------------------------
    // Register a child -> Booking.TotalAmount reflects the excursion cost
    // ----------------------------------------------------------------------------------------

    [Fact]
    public async Task RegisterChild_ViaRegistrationsPage_BumpsBookingTotalAmount()
    {
        var p = SeedActivityWithBooking(withBooking: true);
        const decimal cost = 15m;

        var seeded = _fx.Factory.Seed(ctx =>
        {
            var excursion = new Excursion
            {
                Name = $"Excur-Reg-{Guid.NewGuid():N}",
                ExcursionDate = p.ExcursionDate,
                Cost = cost,
                Type = ExcursionType.Pool,
                ActivityId = p.ActivityId,
                IsActive = true,
                ExcursionGroups = { new ExcursionGroup { ActivityGroupId = p.GroupId } }
            };
            ctx.Excursions.Add(excursion);
            ctx.SaveChanges();
            return excursion.Id;
        });

        decimal totalBefore;
        await using (var pre = _fx.Factory.NewDbContext())
        {
            totalBefore = (await pre.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == p.BookingId)).TotalAmount;
        }

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Excursions/Registrations/{seeded}");

        var checkbox = page.Locator($".registration-checkbox[data-booking-id='{p.BookingId}']");
        (await checkbox.CountAsync()).Should().Be(1, "the eligible child must be listed for registration");

        // Ticking the box fires the AJAX RegisterChild call, then the page reloads after ~500ms.
        await checkbox.CheckAsync();
        await page.WaitForURLAsync("**/Excursions/Registrations/**", new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The checkbox should now be persisted as checked after the reload.
        await Assertions.Expect(page.Locator($".registration-checkbox[data-booking-id='{p.BookingId}']"))
            .ToBeCheckedAsync(new() { Timeout = 10000 });

        await using var db = _fx.Factory.NewDbContext();
        var registered = await db.ExcursionRegistrations.IgnoreQueryFilters()
            .AnyAsync(r => r.ExcursionId == seeded && r.BookingId == p.BookingId);
        registered.Should().BeTrue("registering the child must create an ExcursionRegistration");

        var booking = await db.Bookings.IgnoreQueryFilters().FirstAsync(b => b.Id == p.BookingId);
        booking.TotalAmount.Should().Be(totalBefore + cost,
            "registering a child for an excursion adds the excursion cost to the booking total");
    }
}
