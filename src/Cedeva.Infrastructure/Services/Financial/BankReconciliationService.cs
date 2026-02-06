using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Services.Financial;

public class BankReconciliationService : IBankReconciliationService
{
    // Reconciliation confidence threshold (minimum score to suggest a match)
    private const int MinimumConfidenceScore = 50;

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

    public async Task<List<ReconciliationSuggestionDto>> GetReconciliationSuggestionsAsync(int organisationId)
    {
        var suggestions = new List<ReconciliationSuggestionDto>();

        // Récupérer les transactions non rapprochées
        var transactions = await _context.BankTransactions
            .Where(bt => bt.OrganisationId == organisationId && !bt.IsReconciled && bt.Amount > 0)
            .ToListAsync();

        // Récupérer les réservations non payées avec leurs relations
        var bookings = await _context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .Where(b => b.Activity.OrganisationId == organisationId &&
                       b.PaymentStatus != PaymentStatus.Paid &&
                       b.PaymentStatus != PaymentStatus.Overpaid &&
                       b.IsConfirmed)
            .ToListAsync();

        // Pour chaque transaction, chercher les bookings qui pourraient correspondre
        foreach (var transaction in transactions)
        {
            foreach (var booking in bookings)
            {
                var suggestion = CalculateReconciliationMatch(transaction, booking);

                // Ajouter seulement si le score est suffisant
                if (suggestion != null && suggestion.ConfidenceScore >= MinimumConfidenceScore)
                {
                    suggestions.Add(suggestion);
                }
            }
        }

        // Trier par score décroissant
        return suggestions.OrderByDescending(s => s.ConfidenceScore).ToList();
    }

    /// <summary>
    /// Calcule le score de correspondance entre une transaction et une réservation.
    /// </summary>
    private ReconciliationSuggestionDto? CalculateReconciliationMatch(BankTransaction transaction, Booking booking)
    {
        var score = 0;
        var reasons = new List<string>();
        var remainingAmount = booking.TotalAmount - booking.PaidAmount;

        // Vérifier la correspondance du montant
        score += CalculateAmountMatchScore(transaction.Amount, remainingAmount, reasons);

        // Vérifier la correspondance du nom
        score += CalculateNameMatchScore(transaction.CounterpartyName, booking.Child.Parent, reasons);

        // Vérifier la proximité des dates
        score += CalculateDateMatchScore(transaction.TransactionDate, booking.Activity.StartDate, reasons);

        if (score < 50)
        {
            return null;
        }

        return new ReconciliationSuggestionDto
        {
            TransactionId = transaction.Id,
            BookingId = booking.Id,
            TransactionDate = transaction.TransactionDate,
            TransactionAmount = transaction.Amount,
            CounterpartyName = transaction.CounterpartyName,
            ChildName = $"{booking.Child.FirstName} {booking.Child.LastName}",
            ParentName = $"{booking.Child.Parent.FirstName} {booking.Child.Parent.LastName}",
            ActivityName = booking.Activity.Name,
            BookingRemainingAmount = remainingAmount,
            ConfidenceScore = score,
            MatchReasons = reasons
        };
    }

    private static int CalculateAmountMatchScore(decimal transactionAmount, decimal remainingAmount, List<string> reasons)
    {
        if (transactionAmount == remainingAmount)
        {
            reasons.Add("Montant exact");
            return 50;
        }

        if (Math.Abs(transactionAmount - remainingAmount) <= remainingAmount * 0.05m)
        {
            reasons.Add("Montant proche");
            return 30;
        }

        return 0;
    }

    private static int CalculateNameMatchScore(string? counterpartyName, Parent parent, List<string> reasons)
    {
        if (string.IsNullOrWhiteSpace(counterpartyName))
        {
            return 0;
        }

        var counterpartyLower = counterpartyName.ToLowerInvariant();
        var parentLastName = parent.LastName.ToLowerInvariant();
        var parentFirstName = parent.FirstName.ToLowerInvariant();

        if (counterpartyLower.Contains(parentLastName) && counterpartyLower.Contains(parentFirstName))
        {
            reasons.Add("Nom et prénom du parent");
            return 30;
        }

        if (counterpartyLower.Contains(parentLastName))
        {
            reasons.Add("Nom du parent");
            return 20;
        }

        if (counterpartyLower.Contains(parentFirstName))
        {
            reasons.Add("Prénom du parent");
            return 10;
        }

        return 0;
    }

    private static int CalculateDateMatchScore(DateTime transactionDate, DateTime activityStartDate, List<string> reasons)
    {
        var daysDifference = Math.Abs((transactionDate - activityStartDate).TotalDays);

        if (daysDifference <= 14)
        {
            reasons.Add($"Date proche de l'activité ({daysDifference:F0} jours)");
            return 10;
        }

        return 0;
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
