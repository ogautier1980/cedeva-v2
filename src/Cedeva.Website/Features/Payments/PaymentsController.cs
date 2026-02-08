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
        var activityId = HttpContext.Session.GetInt32("FinancialActivityId");

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

        try
        {
            var booking = await _context.Bookings.FindAsync(viewModel.BookingId);
            if (booking == null)
            {
                TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.BookingNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            var payment = new Payment
            {
                BookingId = viewModel.BookingId,
                Amount = viewModel.Amount,
                PaymentDate = viewModel.PaymentDate,
                PaymentMethod = viewModel.PaymentMethod,
                Status = PaymentStatus.Paid,
                Reference = viewModel.Reference
            };

            _context.Payments.Add(payment);

            UpdateBookingPaymentStatus(booking, viewModel.Amount);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Manual payment created: {Amount} for booking {BookingId}", viewModel.Amount, viewModel.BookingId);

            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.PaymentCreated"].Value;

            return RedirectToAction("Details", "Bookings", new { id = viewModel.BookingId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while creating manual payment for booking {BookingId}", viewModel.BookingId);
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[LocalizerKeyPaymentCreationFailed].Value;
            return RedirectToAction(nameof(Create), new { bookingId = viewModel.BookingId });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while creating manual payment for booking {BookingId}", viewModel.BookingId);
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[LocalizerKeyPaymentCreationFailed].Value;
            return RedirectToAction(nameof(Create), new { bookingId = viewModel.BookingId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating manual payment for booking {BookingId}", viewModel.BookingId);
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[LocalizerKeyPaymentCreationFailed].Value;
            return RedirectToAction(nameof(Create), new { bookingId = viewModel.BookingId });
        }
    }

    // GET: Payments/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Child)
                    .ThenInclude(c => c.Parent)
            .Include(p => p.Booking.Activity)
            .Include(p => p.BankTransaction)
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
        try
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
            {
                TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.PaymentNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            // Ne pas permettre l'annulation des paiements liés à une transaction bancaire
            if (payment.BankTransactionId.HasValue)
            {
                TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Error.CannotCancelBankPayment"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            // Marquer comme annulé
            payment.Status = PaymentStatus.Cancelled;

            // Mettre à jour le montant payé de la réservation
            payment.Booking.PaidAmount -= payment.Amount;

            // Recalculer le statut de paiement
            if (payment.Booking.PaidAmount <= 0)
            {
                payment.Booking.PaymentStatus = PaymentStatus.NotPaid;
            }
            else if (payment.Booking.PaidAmount < payment.Booking.TotalAmount)
            {
                payment.Booking.PaymentStatus = PaymentStatus.PartiallyPaid;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment cancelled: {PaymentId}", id);

            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.PaymentCancelled"].Value;

            return RedirectToAction("Details", "Bookings", new { id = payment.BookingId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while cancelling payment {PaymentId}", id);
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[LocalizerKeyPaymentCancellationFailed].Value;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while cancelling payment {PaymentId}", id);
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[LocalizerKeyPaymentCancellationFailed].Value;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while cancelling payment {PaymentId}", id);
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer[LocalizerKeyPaymentCancellationFailed].Value;
            return RedirectToAction(nameof(Details), new { id });
        }
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

    private static void UpdateBookingPaymentStatus(Booking booking, decimal paymentAmount)
    {
        booking.PaidAmount += paymentAmount;

        if (booking.PaidAmount >= booking.TotalAmount)
        {
            booking.PaymentStatus = booking.PaidAmount > booking.TotalAmount
                ? PaymentStatus.Overpaid
                : PaymentStatus.Paid;
        }
        else if (booking.PaidAmount > 0)
        {
            booking.PaymentStatus = PaymentStatus.PartiallyPaid;
        }
    }
}
