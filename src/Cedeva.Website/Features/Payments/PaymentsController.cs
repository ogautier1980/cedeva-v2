using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Payments.ViewModels;
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

    private const string TempDataSuccess = "Success";
    private const string TempDataError = "Error";

    public PaymentsController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
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
            // Recharger les informations pour l'affichage
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

            return View(viewModel);
        }

        try
        {
            var booking = await _context.Bookings.FindAsync(viewModel.BookingId);
            if (booking == null)
            {
                TempData[TempDataError] = _localizer["Error.BookingNotFound"].Value;
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

            // Mettre à jour le montant payé et le statut de la réservation
            booking.PaidAmount += viewModel.Amount;

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

            await _context.SaveChangesAsync();

            _logger.LogInformation("Manual payment created: {Amount} for booking {BookingId}", viewModel.Amount, viewModel.BookingId);

            TempData[TempDataSuccess] = _localizer["Message.PaymentCreated"].Value;

            return RedirectToAction("Details", "Bookings", new { id = viewModel.BookingId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manual payment");
            TempData[TempDataError] = _localizer["Error.PaymentCreationFailed"].Value;
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
                TempData[TempDataError] = _localizer["Error.PaymentNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            // Ne pas permettre l'annulation des paiements liés à une transaction bancaire
            if (payment.BankTransactionId.HasValue)
            {
                TempData[TempDataError] = _localizer["Error.CannotCancelBankPayment"].Value;
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

            TempData[TempDataSuccess] = _localizer["Message.PaymentCancelled"].Value;

            return RedirectToAction("Details", "Bookings", new { id = payment.BookingId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling payment");
            TempData[TempDataError] = _localizer["Error.PaymentCancellationFailed"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
