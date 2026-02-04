using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services;

public class ExcursionService : IExcursionService
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ExcursionService(CedevaDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Excursion?> GetByIdAsync(int id)
    {
        return await _context.Excursions
            .Include(e => e.Activity)
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<Excursion>> GetByActivityIdAsync(int activityId)
    {
        return await _context.Excursions
            .Include(e => e.ExcursionGroups)
                .ThenInclude(eg => eg.ActivityGroup)
            .Where(e => e.ActivityId == activityId && e.IsActive)
            .OrderBy(e => e.ExcursionDate)
            .ToListAsync();
    }

    public async Task<ExcursionRegistration> RegisterChildAsync(int excursionId, int bookingId)
    {
        var excursion = await _context.Excursions
            .Include(e => e.ExcursionGroups)
            .FirstOrDefaultAsync(e => e.Id == excursionId);

        if (excursion == null)
            throw new InvalidOperationException("Excursion not found");

        var booking = await _context.Bookings
            .Include(b => b.Child)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            throw new InvalidOperationException("Booking not found");

        // Validation: child's group must be in excursion's target groups
        var excursionGroupIds = excursion.ExcursionGroups
            .Select(eg => eg.ActivityGroupId)
            .ToList();

        if (booking.GroupId == null || !excursionGroupIds.Contains(booking.GroupId.Value))
            throw new InvalidOperationException("Child's group not eligible for this excursion");

        // Check for duplicate
        var existing = await _context.ExcursionRegistrations
            .FirstOrDefaultAsync(er => er.ExcursionId == excursionId && er.BookingId == bookingId);

        if (existing != null)
            throw new InvalidOperationException("Child already registered for this excursion");

        // Create registration
        var registration = new ExcursionRegistration
        {
            ExcursionId = excursionId,
            BookingId = bookingId,
            RegistrationDate = DateTime.Now,
            IsPresent = false
        };
        _context.ExcursionRegistrations.Add(registration);

        // Update booking total
        booking.TotalAmount += excursion.Cost;
        booking.PaymentStatus = CalculatePaymentStatus(booking.PaidAmount, booking.TotalAmount);

        // Log financial transaction
        var transaction = new ActivityFinancialTransaction
        {
            ActivityId = excursion.ActivityId,
            TransactionDate = DateTime.Now,
            Type = TransactionType.Income,
            Category = TransactionCategory.ExcursionPayment,
            Amount = excursion.Cost,
            Description = $"Inscription excursion: {excursion.Name} - {booking.Child.FirstName} {booking.Child.LastName}"
        };
        _context.ActivityFinancialTransactions.Add(transaction);

        await _context.SaveChangesAsync();
        return registration;
    }

    public async Task<bool> UnregisterChildAsync(int excursionId, int bookingId)
    {
        var registration = await _context.ExcursionRegistrations
            .Include(er => er.Excursion)
            .Include(er => er.Booking)
                .ThenInclude(b => b.Child)
            .FirstOrDefaultAsync(er => er.ExcursionId == excursionId && er.BookingId == bookingId);

        if (registration == null)
            return false;

        var excursion = registration.Excursion;
        var booking = registration.Booking;

        // Remove registration
        _context.ExcursionRegistrations.Remove(registration);

        // Update booking total
        booking.TotalAmount -= excursion.Cost;
        booking.PaymentStatus = CalculatePaymentStatus(booking.PaidAmount, booking.TotalAmount);

        // Log compensating financial transaction
        var transaction = new ActivityFinancialTransaction
        {
            ActivityId = excursion.ActivityId,
            TransactionDate = DateTime.Now,
            Type = TransactionType.Income,
            Category = TransactionCategory.ExcursionPayment,
            Amount = -excursion.Cost, // Negative amount for reversal
            Description = $"Désinscription excursion: {excursion.Name} - {booking.Child.FirstName} {booking.Child.LastName}"
        };
        _context.ActivityFinancialTransactions.Add(transaction);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ExcursionRegistration>> GetRegistrationsAsync(int excursionId)
    {
        return await _context.ExcursionRegistrations
            .Include(er => er.Booking)
                .ThenInclude(b => b.Child)
            .Include(er => er.Booking)
                .ThenInclude(b => b.Group)
            .Where(er => er.ExcursionId == excursionId)
            .ToListAsync();
    }

    public async Task<bool> UpdateAttendanceAsync(int registrationId, bool isPresent)
    {
        var registration = await _context.ExcursionRegistrations
            .FirstOrDefaultAsync(er => er.Id == registrationId);

        if (registration == null)
            return false;

        registration.IsPresent = isPresent;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ExcursionFinancialSummary> GetFinancialSummaryAsync(int excursionId)
    {
        var excursion = await _context.Excursions
            .Include(e => e.Registrations)
            .Include(e => e.Expenses)
            .FirstOrDefaultAsync(e => e.Id == excursionId);

        if (excursion == null)
            throw new InvalidOperationException("Excursion not found");

        var registrationCount = excursion.Registrations.Count;
        var totalRevenue = registrationCount * excursion.Cost;
        var totalExpenses = excursion.Expenses.Sum(ex => ex.Amount);

        return new ExcursionFinancialSummary
        {
            RegistrationCount = registrationCount,
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            NetBalance = totalRevenue - totalExpenses
        };
    }

    private static PaymentStatus CalculatePaymentStatus(decimal paidAmount, decimal totalAmount)
    {
        if (paidAmount == 0)
            return PaymentStatus.NotPaid;
        if (paidAmount < totalAmount)
            return PaymentStatus.PartiallyPaid;
        if (paidAmount == totalAmount)
            return PaymentStatus.Paid;
        return PaymentStatus.Overpaid;
    }
}
