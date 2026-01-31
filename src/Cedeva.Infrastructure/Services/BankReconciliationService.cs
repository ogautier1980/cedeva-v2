using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Services;

public class BankReconciliationService : IBankReconciliationService
{
    private readonly CedevaDbContext _context;
    private readonly IStructuredCommunicationService _structuredCommunicationService;
    private readonly ILogger<BankReconciliationService> _logger;

    public BankReconciliationService(
        CedevaDbContext context,
        IStructuredCommunicationService structuredCommunicationService,
        ILogger<BankReconciliationService> logger)
    {
        _context = context;
        _structuredCommunicationService = structuredCommunicationService;
        _logger = logger;
    }

    public async Task<int> AutoReconcileTransactionsAsync(int codaFileId)
    {
        var reconciledCount = 0;

        // Récupérer les transactions non rapprochées de ce fichier CODA
        var unreconciledTransactions = await _context.BankTransactions
            .Where(bt => bt.CodaFileId == codaFileId && !bt.IsReconciled && bt.Amount > 0) // Only credit transactions
            .ToListAsync();

        foreach (var transaction in unreconciledTransactions)
        {
            // Si pas de communication structurée, on ne peut pas faire de matching automatique
            if (string.IsNullOrWhiteSpace(transaction.StructuredCommunication))
                continue;

            // Valider la communication structurée
            if (!_structuredCommunicationService.ValidateStructuredCommunication(transaction.StructuredCommunication))
            {
                _logger.LogWarning("Invalid structured communication: {Communication}", transaction.StructuredCommunication);
                continue;
            }

            // Extraire l'ID de réservation
            var bookingId = _structuredCommunicationService.ExtractBookingIdFromCommunication(transaction.StructuredCommunication);
            if (!bookingId.HasValue)
            {
                _logger.LogWarning("Could not extract booking ID from communication: {Communication}", transaction.StructuredCommunication);
                continue;
            }

            // Trouver la réservation
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId.Value);

            if (booking == null)
            {
                _logger.LogWarning("Booking not found for ID: {BookingId}", bookingId.Value);
                continue;
            }

            // Vérifier que la communication structurée correspond
            if (booking.StructuredCommunication != transaction.StructuredCommunication)
            {
                _logger.LogWarning("Structured communication mismatch for booking {BookingId}. Expected: {Expected}, Got: {Got}",
                    bookingId.Value, booking.StructuredCommunication, transaction.StructuredCommunication);
                continue;
            }

            // Rapprocher la transaction
            var reconciled = await ReconcileTransactionAsync(transaction, booking);
            if (reconciled)
            {
                reconciledCount++;
            }
        }

        if (reconciledCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Auto-reconciled {Count} transactions from CODA file {CodaFileId}", reconciledCount, codaFileId);
        }

        return reconciledCount;
    }

    public async Task<bool> ManualReconcileAsync(int transactionId, int bookingId)
    {
        var transaction = await _context.BankTransactions
            .FirstOrDefaultAsync(bt => bt.Id == transactionId);

        if (transaction == null)
        {
            _logger.LogWarning("Bank transaction not found: {TransactionId}", transactionId);
            return false;
        }

        if (transaction.IsReconciled)
        {
            _logger.LogWarning("Transaction {TransactionId} is already reconciled", transactionId);
            return false;
        }

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            _logger.LogWarning("Booking not found: {BookingId}", bookingId);
            return false;
        }

        var reconciled = await ReconcileTransactionAsync(transaction, booking);
        if (reconciled)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Manually reconciled transaction {TransactionId} with booking {BookingId}",
                transactionId, bookingId);
        }

        return reconciled;
    }

    public async Task<List<UnreconciledTransactionDto>> GetUnreconciledTransactionsAsync(int organisationId)
    {
        return await _context.BankTransactions
            .Where(bt => bt.OrganisationId == organisationId && !bt.IsReconciled && bt.Amount > 0)
            .OrderByDescending(bt => bt.TransactionDate)
            .Select(bt => new UnreconciledTransactionDto
            {
                Id = bt.Id,
                TransactionDate = bt.TransactionDate,
                Amount = bt.Amount,
                StructuredCommunication = bt.StructuredCommunication,
                FreeCommunication = bt.FreeCommunication,
                CounterpartyName = bt.CounterpartyName,
                CounterpartyAccount = bt.CounterpartyAccount,
                AccountNumber = bt.CodaFile.AccountNumber
            })
            .ToListAsync();
    }

    public async Task<List<UnpaidBookingDto>> GetUnpaidBookingsAsync(int organisationId)
    {
        return await _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .Where(b => b.Activity.OrganisationId == organisationId &&
                       b.PaymentStatus != PaymentStatus.Paid &&
                       b.PaymentStatus != PaymentStatus.Overpaid &&
                       b.IsConfirmed)
            .OrderBy(b => b.Activity.StartDate)
            .Select(b => new UnpaidBookingDto
            {
                Id = b.Id,
                StructuredCommunication = b.StructuredCommunication,
                TotalAmount = b.TotalAmount,
                PaidAmount = b.PaidAmount,
                ChildName = b.Child.FirstName + " " + b.Child.LastName,
                ParentName = b.Child.Parent.FirstName + " " + b.Child.Parent.LastName,
                ActivityName = b.Activity.Name,
                ActivityStartDate = b.Activity.StartDate
            })
            .ToListAsync();
    }

    /// <summary>
    /// Rapproche une transaction avec une réservation en créant un Payment
    /// et en mettant à jour les statuts.
    /// </summary>
    private async Task<bool> ReconcileTransactionAsync(BankTransaction transaction, Booking booking)
    {
        try
        {
            // Créer le paiement
            var payment = new Payment
            {
                BookingId = booking.Id,
                Amount = transaction.Amount,
                PaymentDate = transaction.TransactionDate,
                PaymentMethod = PaymentMethod.BankTransfer,
                Status = PaymentStatus.Paid,
                StructuredCommunication = transaction.StructuredCommunication,
                BankTransactionId = transaction.Id
            };

            _context.Payments.Add(payment);

            // Marquer la transaction comme rapprochée
            transaction.IsReconciled = true;
            transaction.PaymentId = payment.Id; // Will be set after SaveChanges

            // Mettre à jour le montant payé et le statut de la réservation
            booking.PaidAmount += transaction.Amount;

            // Calculer le nouveau statut de paiement
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
            else
            {
                booking.PaymentStatus = PaymentStatus.NotPaid;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling transaction {TransactionId} with booking {BookingId}",
                transaction.Id, booking.Id);
            return false;
        }
    }
}
