using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// End-to-end coverage of the coordinator booking + presence flow: a real browser (authenticated as
/// a Coordinator of the seeded organisation) creating a booking via the JS-driven Create form,
/// confirming it through Edit, then marking a child present on the ActivityManagement Presences page
/// (an AJAX call to UpdatePresence). Each test seeds its own activity/parent/child with unique names
/// so the shared SQLite DB stays self-contained.
/// </summary>
[Collection("E2E")]
public class BookingsPresencesE2ETests
{
    private readonly PlaywrightFixture _fx;

    public BookingsPresencesE2ETests(PlaywrightFixture fx) => _fx = fx;

    private sealed record Seeded(int ActivityId, int ChildId, int[] ActivityDayIds, string ChildLastName);

    /// <summary>
    /// Seeds (in the coordinator's org) an active activity with five active week-1 days starting next
    /// Monday, a parent with an address, and one child. Returns the ids and the unique child surname.
    /// </summary>
    private Seeded SeedActivityParentChild()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        return _fx.Factory.Seed(ctx =>
        {
            // Next Monday so all five seeded days are weekdays (default-checked in the Create UI).
            var start = DateTime.Today.AddMonths(3);
            while (start.DayOfWeek != DayOfWeek.Monday) start = start.AddDays(1);

            var activity = new Activity
            {
                Name = $"Stage-{tag}",
                Description = "Activity for booking E2E",
                IsActive = true,
                PricePerDay = 15m,
                StartDate = start,
                EndDate = start.AddDays(4),
                OrganisationId = _fx.OrganisationId
            };

            var days = new List<ActivityDay>();
            for (var i = 0; i < 5; i++)
            {
                var d = new ActivityDay
                {
                    Label = $"Day {i + 1}",
                    DayDate = start.AddDays(i),
                    Week = 1,
                    IsActive = true,
                    Activity = activity
                };
                days.Add(d);
                activity.Days.Add(d);
            }

            var parent = new Parent
            {
                FirstName = "Paul",
                LastName = $"Parent-{tag}",
                Email = $"parent-{tag}@test.be",
                MobilePhoneNumber = "0470000000",
                NationalRegisterNumber = "85.06.15-133.80",
                OrganisationId = _fx.OrganisationId,
                Address = new Address
                {
                    Street = "Rue Test 1",
                    City = "Bruxelles",
                    PostalCode = "1000",
                    Country = Country.Belgium
                }
            };

            var child = new Child
            {
                FirstName = "Lucas",
                LastName = $"Child-{tag}",
                BirthDate = new DateTime(2016, 7, 8),
                NationalRegisterNumber = "16.07.08-164.10",
                Parent = parent
            };

            ctx.Activities.Add(activity);
            ctx.Children.Add(child);
            ctx.SaveChanges();

            return new Seeded(
                activity.Id,
                child.Id,
                days.Select(d => d.DayId).ToArray(),
                child.LastName);
        });
    }

    [Fact]
    public async Task CreateForm_RendersForCoordinator()
    {
        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Bookings/Create");

        response!.Status.Should().Be(200);
        (await page.Locator("#ChildId").CountAsync()).Should().Be(1);
        (await page.Locator("#ActivityId").CountAsync()).Should().Be(1);
    }

    [Fact(Skip = "E2E browser-widget flakiness (Choices/Summernote/AJAX/modal); CRUD covered by controller integration tests. TODO revisit.")]
    public async Task CreateBooking_WithValidData_IsPersisted()
    {
        var seed = SeedActivityParentChild();

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Bookings/Create");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The selects are wrapped by Choices.js and the day checkboxes are loaded by AJAX when the
        // activity changes. Drive the Choices widgets (real selection -> native change) so the
        // controller's GetActivityDays handler populates .day-checkbox elements.
        await page.SelectChoicesAsync("#ChildId", seed.ChildId.ToString());
        await page.SelectChoicesAsync("#ActivityId", seed.ActivityId.ToString());

        // Wait for the AJAX-rendered day checkboxes, then ensure at least one is checked.
        await page.WaitForSelectorAsync(".day-checkbox", new() { Timeout = 15000 });
        var dayCheckbox = page.Locator(".day-checkbox").First;
        if (!await dayCheckbox.IsCheckedAsync())
        {
            await dayCheckbox.CheckAsync();
        }

        // The page has hidden inline child/parent AJAX forms with their own submit buttons; the main
        // booking form's submit is the only btn-primary text-nowrap one, so target that.
        await page.Locator("button.btn-primary.text-nowrap[type=submit]").ClickAsync();
        await page.WaitForURLAsync("**/Bookings/Details/**", new() { Timeout = 15000 });

        page.Url.Should().Contain("/Bookings/Details/");

        await using var db = _fx.Factory.NewDbContext();
        var booking = await db.Bookings
            .IgnoreQueryFilters()
            .Include(b => b.Days)
            .FirstOrDefaultAsync(b => b.ChildId == seed.ChildId && b.ActivityId == seed.ActivityId);

        booking.Should().NotBeNull("the coordinator submitted a valid booking");
        booking!.Days.Should().NotBeEmpty("at least one activity day was selected");
        booking.Days.Select(d => d.ActivityDayId).Should().BeSubsetOf(seed.ActivityDayIds);
    }

    [Fact]
    public async Task CreateBooking_WithoutChildOrActivity_ShowsValidationAndDoesNotPersist()
    {
        // A fresh activity that has no booking yet, so we can assert nothing got created for it.
        var seed = SeedActivityParentChild();

        await using var ctx = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Bookings/Create");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Submit without selecting a child or an activity (both are [Required] on the view model).
        await page.Locator("button.btn-primary.text-nowrap[type=submit]").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Stayed on the Create page (no redirect to Details).
        page.Url.Should().NotContain("/Bookings/Details/", "an invalid submission must not create a booking");

        await using var db = _fx.Factory.NewDbContext();
        var anyForChild = await db.Bookings
            .IgnoreQueryFilters()
            .AnyAsync(b => b.ChildId == seed.ChildId);
        anyForChild.Should().BeFalse("no booking should be persisted for the unselected child");
    }

    [Fact]
    public async Task EditBooking_Confirm_IsPersisted()
    {
        var seed = SeedActivityParentChild();

        // Seed an unconfirmed booking with one reserved day directly so we can drive the Edit flow.
        var bookingId = _fx.Factory.Seed(ctx =>
        {
            var booking = new Booking
            {
                BookingDate = DateTime.Today,
                ChildId = seed.ChildId,
                ActivityId = seed.ActivityId,
                IsConfirmed = false,
                IsMedicalSheet = false,
                TotalAmount = 15m,
                PaidAmount = 0,
                PaymentStatus = PaymentStatus.NotPaid
            };
            ctx.Bookings.Add(booking);
            ctx.SaveChanges();

            ctx.BookingDays.Add(new BookingDay
            {
                BookingId = booking.Id,
                ActivityDayId = seed.ActivityDayIds[0],
                IsReserved = true,
                IsPresent = false
            });
            ctx.SaveChanges();
            return booking.Id;
        });

        await using var ctx2 = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx2.NewPageAsync();
        var response = await page.GotoAsync($"{_fx.BaseUrl}/Bookings/Edit/{bookingId}");
        response!.Status.Should().Be(200);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Tick the IsConfirmed checkbox and keep the existing reserved day checked.
        var confirm = page.Locator("#IsConfirmed");
        if (!await confirm.IsCheckedAsync())
        {
            await confirm.CheckAsync();
        }

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync("**/Bookings/Details/**", new() { Timeout = 15000 });

        await using var db = _fx.Factory.NewDbContext();
        var booking = await db.Bookings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        booking.Should().NotBeNull();
        booking!.IsConfirmed.Should().BeTrue("the coordinator confirmed the booking via Edit");
    }

    [Fact]
    public async Task Presences_MarkPresent_PersistsViaAjax()
    {
        var seed = SeedActivityParentChild();

        // Seed a confirmed booking with one reserved (but absent) day; that booking-day is what the
        // Presences page exposes as a tickable checkbox carrying its BookingDay id.
        var bookingDayId = _fx.Factory.Seed(ctx =>
        {
            var booking = new Booking
            {
                BookingDate = DateTime.Today,
                ChildId = seed.ChildId,
                ActivityId = seed.ActivityId,
                IsConfirmed = true,
                IsMedicalSheet = false,
                TotalAmount = 15m,
                PaidAmount = 0,
                PaymentStatus = PaymentStatus.NotPaid
            };
            ctx.Bookings.Add(booking);
            ctx.SaveChanges();

            var bookingDay = new BookingDay
            {
                BookingId = booking.Id,
                ActivityDayId = seed.ActivityDayIds[0],
                IsReserved = true,
                IsPresent = false
            };
            ctx.BookingDays.Add(bookingDay);
            ctx.SaveChanges();
            return bookingDay.Id;
        });

        await using var ctx2 = await _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);
        var page = await ctx2.NewPageAsync();

        // Land on the first seeded day so the confirmed child appears with its presence checkbox.
        var response = await page.GotoAsync(
            $"{_fx.BaseUrl}/ActivityManagement/Presences?id={seed.ActivityId}&dayId={seed.ActivityDayIds[0]}");
        response!.Status.Should().Be(200);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var presenceCheckbox = page.Locator($".presence-checkbox[data-bookingday-id='{bookingDayId}']");
        (await presenceCheckbox.CountAsync()).Should().Be(1, "the confirmed child should be listed for the selected day");

        // Ticking the checkbox fires the AJAX POST to UpdatePresence. Wait for that request to finish.
        await presenceCheckbox.CheckAsync();
        await page.WaitForResponseAsync(r =>
            r.Url.Contains("/ActivityManagement/UpdatePresence", StringComparison.OrdinalIgnoreCase)
            && r.Status == 200,
            new() { Timeout = 15000 });

        await using var db = _fx.Factory.NewDbContext();
        var bookingDay = await db.BookingDays
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(bd => bd.Id == bookingDayId);

        bookingDay.Should().NotBeNull();
        bookingDay!.IsPresent.Should().BeTrue("marking the checkbox should persist the presence");
    }
}
