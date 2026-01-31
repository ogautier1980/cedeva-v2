using Cedeva.Core.Interfaces;

namespace Cedeva.Website.Features.Financial.ViewModels;

/// <summary>
/// ViewModel for the bank reconciliation page showing unreconciled transactions
/// and unpaid bookings side by side.
/// </summary>
public class ReconciliationViewModel
{
    public List<UnreconciledTransactionDto> UnreconciledTransactions { get; set; } = new();
    public List<UnpaidBookingDto> UnpaidBookings { get; set; } = new();

    public int TotalUnreconciledCount => UnreconciledTransactions.Count;
    public int TotalUnpaidCount => UnpaidBookings.Count;
    public decimal TotalUnreconciledAmount => UnreconciledTransactions.Sum(t => t.Amount);
    public decimal TotalUnpaidAmount => UnpaidBookings.Sum(b => b.RemainingAmount);
}

/// <summary>
/// ViewModel for manually reconciling a transaction with a booking.
/// </summary>
public class ManualReconcileViewModel
{
    public int TransactionId { get; set; }
    public int BookingId { get; set; }
}
