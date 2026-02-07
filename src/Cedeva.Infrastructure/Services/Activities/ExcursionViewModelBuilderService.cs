using Cedeva.Core.DTOs.Excursions;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Activities;

/// <summary>
/// Service for building complex ViewModels for excursion views.
/// Encapsulates data retrieval, grouping, sorting, and transformation logic.
/// </summary>
public class ExcursionViewModelBuilderService : IExcursionViewModelBuilderService
{
    private readonly CedevaDbContext _context;
    private readonly IExcursionService _excursionService;

    public ExcursionViewModelBuilderService(
        CedevaDbContext context,
        IExcursionService excursionService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _excursionService = excursionService ?? throw new ArgumentNullException(nameof(excursionService));
    }

    public async Task<Dictionary<ActivityGroup, List<ExcursionChildInfo>>> BuildRegistrationsByGroupAsync(
        int excursionId,
        Func<PaymentStatus, string> paymentStatusLocalizer)
    {
        // Get excursion with related data
        var excursion = await _context.Excursions
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .FirstOrDefaultAsync(e => e.Id == excursionId);

        if (excursion == null)
            return new Dictionary<ActivityGroup, List<ExcursionChildInfo>>();

        // Get eligible group IDs
        var eligibleGroupIds = excursion.ExcursionGroups
            .Select(eg => eg.ActivityGroupId)
            .ToList();

        // Get all confirmed bookings for eligible groups
        var bookings = await _context.Bookings
            .Include(b => b.Child)
            .Include(b => b.Group)
            .Where(b => b.ActivityId == excursion.ActivityId &&
                       b.IsConfirmed &&
                       b.GroupId.HasValue &&
                       eligibleGroupIds.Contains(b.GroupId.Value))
            .ToListAsync();

        // Get existing registrations
        var registrations = await _excursionService.GetRegistrationsAsync(excursionId);
        var registrationsByBookingId = registrations.ToDictionary(r => r.BookingId, r => r);

        // Group children by ActivityGroup
        var childrenByGroup = new Dictionary<ActivityGroup, List<ExcursionChildInfo>>();

        foreach (var booking in bookings.Where(b => b.Group != null))
        {
            if (!childrenByGroup.ContainsKey(booking.Group!))
            {
                childrenByGroup[booking.Group!] = new List<ExcursionChildInfo>();
            }

            var isRegistered = registrationsByBookingId.ContainsKey(booking.Id);
            var registration = isRegistered ? registrationsByBookingId[booking.Id] : null;

            childrenByGroup[booking.Group!].Add(new ExcursionChildInfo
            {
                BookingId = booking.Id,
                ChildId = booking.ChildId,
                FirstName = booking.Child.FirstName,
                LastName = booking.Child.LastName,
                BirthDate = booking.Child.BirthDate,
                IsRegistered = isRegistered,
                RegistrationId = registration?.Id,
                ExcursionCost = excursion.Cost,
                PaymentStatus = paymentStatusLocalizer(booking.PaymentStatus)
            });
        }

        // Sort children within each group by LastName, FirstName
        foreach (var group in childrenByGroup.Keys.ToList())
        {
            childrenByGroup[group] = childrenByGroup[group]
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToList();
        }

        return childrenByGroup;
    }

    public async Task<Dictionary<ActivityGroup, List<ExcursionAttendanceInfo>>> BuildAttendanceByGroupAsync(int excursionId)
    {
        // Get all registrations with related booking and child data
        var registrations = await _context.ExcursionRegistrations
            .Include(er => er.Booking)
                .ThenInclude(b => b.Child)
            .Include(er => er.Booking)
                .ThenInclude(b => b.Group)
            .Where(er => er.ExcursionId == excursionId && er.Booking.Group != null)
            .ToListAsync();

        // Group children by ActivityGroup
        var childrenByGroup = new Dictionary<ActivityGroup, List<ExcursionAttendanceInfo>>();

        foreach (var registration in registrations)
        {
            var group = registration.Booking.Group!;

            if (!childrenByGroup.ContainsKey(group))
            {
                childrenByGroup[group] = new List<ExcursionAttendanceInfo>();
            }

            childrenByGroup[group].Add(new ExcursionAttendanceInfo
            {
                RegistrationId = registration.Id,
                BookingId = registration.BookingId,
                FirstName = registration.Booking.Child.FirstName,
                LastName = registration.Booking.Child.LastName,
                BirthDate = registration.Booking.Child.BirthDate,
                IsPresent = registration.IsPresent
            });
        }

        // Sort children within each group by LastName, FirstName
        foreach (var group in childrenByGroup.Keys.ToList())
        {
            childrenByGroup[group] = childrenByGroup[group]
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToList();
        }

        return childrenByGroup;
    }
}
