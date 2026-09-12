using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Payments.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Payments;

[Authorize(Roles = "Coordinator,Admin")]
public class PaymentsController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IUserDisplayService _userDisplayService;

    private const string SessionKeyFinancialActivityId = "Financial_ActivityId";
    private const string LocalizerKeyPaymentCreationFailed = "Error.PaymentCreationFailed";
    private const string LocalizerKeyPaymentCancellationFailed = "Error.PaymentCancellationFailed";

    public PaymentsController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILogger<PaymentsController> logger,
        IUserDisplayService userDisplayService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
        _userDisplayService = userDisplayService;
    }

    // GET: Payments
    public async Task<IActionResult> Index(int? activityId, int? bookingId)
    {
        var organisationId = _currentUserService.OrganisationId;

        var query = _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Child)
                    .ThenInclude(c => c.Parent)
            .Include(p => p.Booking.Activity)
            .Where(p => p.Booking.Activity.OrganisationId == organisationId);

        if (activityId.HasValue)
        {
            query = query.Where(p => p.Booking.ActivityId == activityId.Value);
            ViewBag.ActivityId = activityId.Value;

            var activity = await _context.Activities.FindAsync(activityId.Value);
            ViewBag.ActivityName = activity?.Name;
        }

        if (bookingId.HasValue)
        {
            query = query.Where(p => p.BookingId == bookingId.Value);
            ViewBag.BookingId = bookingId.Value;
        }

        var payments = await query
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentListViewModel
            {
                Id = p.Id,
                PaymentDate = p.PaymentDate,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod,
                Status = p.Status,
                Reference = p.Reference,
                ChildName = p.Booking.Child.FirstName + " " + p.Booking.Child.LastName,
                ParentName = p.Booking.Child.Parent.FirstName + " " + p.Booking.Child.Parent.LastName,
                ActivityName = p.Booking.Activity.Name,
                BookingId = p.BookingId
            })
            .ToListAsync();

        return View(payments);
    }

    // GET: Payments/SelectBooking
    public async Task<IActionResult> SelectBooking()
    {
        var organisationId = _currentUserService.OrganisationId;

        // Récupérer l'activité depuis la session (depuis Financial)
        var activityId = HttpContext.Session.GetInt32(SessionKeyFinancialActivityId);

        var query = _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .Where(b => b.Activity.OrganisationId == organisationId);

        if (activityId.HasValue)
        {
            query = query.Where(b => b.ActivityId == activityId.Value);
        }

        // Filtrer les réservations avec paiement incomplet
        var bookings = await query
            .Where(b => b.PaymentStatus == PaymentStatus.NotPaid ||
                       b.PaymentStatus == PaymentStatus.PartiallyPaid)
            .OrderBy(b => b.Child.LastName)
            .ThenBy(b => b.Child.FirstName)
            .Select(b => new
            {
                b.Id,
                ChildName = b.Child.FirstName + " " + b.Child.LastName,
                ParentName = b.Child.Parent.FirstName + " " + b.Child.Parent.LastName,
                ActivityName = b.Activity.Name,
                TotalAmount = b.TotalAmount,
                PaidAmount = b.PaidAmount,
                RemainingAmount = b.TotalAmount - b.PaidAmount,
                PaymentStatus = b.PaymentStatus
            })
            .ToListAsync();

        ViewBag.ActivityId = activityId;
        return View(bookings);
    }

    // GET: Payments/Create?bookingId=5
    public async Task<IActionResult> Create(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            return NotFound();
        }

        var viewModel = new PaymentViewModel
        {
            BookingId = bookingId,
            Amount = booking.TotalAmount - booking.PaidAmount,  // Montant restant par défaut
            PaymentDate = DateTime.Today,
            PaymentMethod = PaymentMethod.Cash,
            ChildName = $"{booking.Child.FirstName} {booking.Child.LastName}",
            ParentName = $"{booking.Child.Parent.FirstName} {booking.Child.Parent.LastName}",
            ActivityName = booking.Activity.Name,
            BookingTotalAmount = booking.TotalAmount,
            BookingPaidAmount = booking.PaidAmount
        };

        return View(viewModel);
    }

    // POST: Payments/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            await ReloadBookingInfoForViewModel(viewModel);
            return View(viewModel);
        }

        var booking = await _context.Bookings.FindAsync(viewModel.BookingId);
        if (booking == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.BookingNotFound"].Value;
            return RedirectToAction(nameof(Index));
        }

        var payment = new Payment
        {
            BookingId = viewModel.BookingId,
            TicketNumber = await GetNextTicketNumberAsync(booking.ActivityId),
            Amount = viewModel.Amount,
            PaymentDate = viewModel.PaymentDate,
            PaymentMethod = viewModel.PaymentMethod,
            Status = PaymentStatus.Paid,
            Reference = viewModel.Reference
        };

        _context.Payments.Add(payment);

        UpdateBookingPaymentStatus(booking, viewModel.Amount);

        // Persistence failures bubble to the global exception handler (/Home/Error).
        await _context.SaveChangesAsync();

        _logger.LogInformation("Manual payment created: {Amount} for booking {BookingId}", viewModel.Amount, viewModel.BookingId);

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.PaymentCreated"].Value;

        return RedirectToAction("Details", "Bookings", new { id = viewModel.BookingId });
    }

    // GET: Payments/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Child)
                    .ThenInclude(c => c.Parent)
            .Include(p => p.Booking.Activity)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            return NotFound();
        }

        // Fetch user display names for audit fields
        ViewBag.CreatedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(payment.CreatedBy);
        if (!string.IsNullOrEmpty(payment.ModifiedBy))
        {
            ViewBag.ModifiedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(payment.ModifiedBy);
        }

        return View(payment);
    }


    // POST: Payments/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.PaymentNotFound"].Value;
            return RedirectToAction(nameof(Index));
        }

        // Marquer comme annulé
        payment.Status = PaymentStatus.Cancelled;

        // Mettre à jour le montant payé de la réservation
        payment.Booking.PaidAmount -= payment.Amount;
        RecalculateBookingPaymentStatus(payment.Booking);

        // Persistence failures bubble to the global exception handler (/Home/Error).
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment cancelled: {PaymentId}", id);

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.PaymentCancelled"].Value;

        return RedirectToAction("Details", "Bookings", new { id = payment.BookingId });
    }

    // GET: Payments/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Child)
                    .ThenInclude(c => c.Parent)
            .Include(p => p.Booking.Activity)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            return NotFound();
        }

        if (payment.Status == PaymentStatus.Cancelled)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.PaymentCancelledCannotEdit"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        var viewModel = new PaymentViewModel
        {
            Id = payment.Id,
            BookingId = payment.BookingId,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod,
            Reference = payment.Reference,
            ChildName = $"{payment.Booking.Child.FirstName} {payment.Booking.Child.LastName}",
            ParentName = $"{payment.Booking.Child.Parent.FirstName} {payment.Booking.Child.Parent.LastName}",
            ActivityName = payment.Booking.Activity.Name,
            BookingTotalAmount = payment.Booking.TotalAmount,
            // "Already paid, excluding this payment" so the sidebar/remaining-balance preview in the
            // shared Edit view reacts correctly as the coordinator adjusts this payment's amount.
            BookingPaidAmount = payment.Booking.PaidAmount - payment.Amount
        };

        return View(viewModel);
    }

    // POST: Payments/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PaymentViewModel viewModel)
    {
        var payment = await _context.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == viewModel.Id);

        if (payment == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.PaymentNotFound"].Value;
            return RedirectToAction(nameof(Index));
        }

        if (payment.Status == PaymentStatus.Cancelled)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.PaymentCancelledCannotEdit"].Value;
            return RedirectToAction(nameof(Details), new { id = viewModel.Id });
        }

        if (!ModelState.IsValid)
        {
            await ReloadBookingInfoForViewModel(viewModel);
            viewModel.BookingPaidAmount = payment.Booking.PaidAmount - payment.Amount;
            return View(viewModel);
        }

        // Réajuster le montant payé de la réservation par la différence (pas un simple ajout).
        payment.Booking.PaidAmount = payment.Booking.PaidAmount - payment.Amount + viewModel.Amount;
        RecalculateBookingPaymentStatus(payment.Booking);

        payment.Amount = viewModel.Amount;
        payment.PaymentDate = viewModel.PaymentDate;
        payment.PaymentMethod = viewModel.PaymentMethod;
        payment.Reference = viewModel.Reference;

        // Persistence failures bubble to the global exception handler (/Home/Error).
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment edited: {PaymentId}, new amount {Amount}", payment.Id, viewModel.Amount);

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.PaymentUpdated"].Value;

        return RedirectToAction("Details", "Bookings", new { id = payment.BookingId });
    }

    // POST: Payments/Delete/5 — physically removes the line (unlike Cancel, which keeps a
    // Cancelled row for the audit trail). Meant for erroneous entries (wrong booking/account…).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.PaymentNotFound"].Value;
            return RedirectToAction(nameof(Index));
        }

        var bookingId = payment.BookingId;

        if (payment.Status != PaymentStatus.Cancelled)
        {
            payment.Booking.PaidAmount -= payment.Amount;
            RecalculateBookingPaymentStatus(payment.Booking);
        }

        _context.Payments.Remove(payment);

        // Persistence failures bubble to the global exception handler (/Home/Error).
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment deleted: {PaymentId}", id);

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.PaymentDeleted"].Value;

        return RedirectToAction("Details", "Bookings", new { id = bookingId });
    }

    private async Task ReloadBookingInfoForViewModel(PaymentViewModel viewModel)
    {
        var booking = await _context.Bookings
            .Include(b => b.Child).ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .FirstOrDefaultAsync(b => b.Id == viewModel.BookingId);

        if (booking != null)
        {
            viewModel.ChildName = $"{booking.Child.FirstName} {booking.Child.LastName}";
            viewModel.ParentName = $"{booking.Child.Parent.FirstName} {booking.Child.Parent.LastName}";
            viewModel.ActivityName = booking.Activity.Name;
            viewModel.BookingTotalAmount = booking.TotalAmount;
            viewModel.BookingPaidAmount = booking.PaidAmount;
        }
    }

    /// <summary>
    /// Next ticket number for the activity's shared Payment/Expense sequence (starts at 1, resets per activity).
    /// </summary>
    private async Task<int> GetNextTicketNumberAsync(int activityId)
    {
        var maxPaymentTicket = await _context.Payments
            .Where(p => p.Booking.ActivityId == activityId)
            .Select(p => (int?)p.TicketNumber)
            .MaxAsync() ?? 0;

        var maxExpenseTicket = await _context.Expenses
            .Where(e => e.ActivityId == activityId)
            .Select(e => (int?)e.TicketNumber)
            .MaxAsync() ?? 0;

        return Math.Max(maxPaymentTicket, maxExpenseTicket) + 1;
    }

    private static void UpdateBookingPaymentStatus(Booking booking, decimal paymentAmount)
    {
        booking.PaidAmount += paymentAmount;
        RecalculateBookingPaymentStatus(booking);
    }

    /// <summary>Derives Booking.PaymentStatus from its current PaidAmount/TotalAmount — shared by
    /// Create/Edit/Cancel/Delete so every mutation of PaidAmount ends in the same status logic.</summary>
    private static void RecalculateBookingPaymentStatus(Booking booking)
    {
        if (booking.PaidAmount <= 0)
        {
            booking.PaymentStatus = PaymentStatus.NotPaid;
        }
        else if (booking.PaidAmount < booking.TotalAmount)
        {
            booking.PaymentStatus = PaymentStatus.PartiallyPaid;
        }
        else
        {
            booking.PaymentStatus = booking.PaidAmount > booking.TotalAmount
                ? PaymentStatus.Overpaid
                : PaymentStatus.Paid;
        }
    }
}
