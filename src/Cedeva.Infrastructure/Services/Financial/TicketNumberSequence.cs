using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Financial;

/// <summary>
/// The activity's shared Payment/Expense ticket sequence (starts at 1, resets per activity). Used by
/// every place that records a line: the accounts screens and the online-payment webhook.
/// </summary>
public static class TicketNumberSequence
{
    /// <summary>
    /// Next ticket number for <paramref name="activityId"/>. Query filters are bypassed so the
    /// anonymous webhook sees the activity's lines too: the caller has already resolved the activity.
    /// </summary>
    public static async Task<int> NextAsync(CedevaDbContext context, int activityId, CancellationToken cancellationToken = default)
    {
        var maxPaymentTicket = await context.Payments.IgnoreQueryFilters()
            .Where(p => p.Booking.ActivityId == activityId)
            .Select(p => (int?)p.TicketNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var maxExpenseTicket = await context.Expenses.IgnoreQueryFilters()
            .Where(e => e.ActivityId == activityId)
            .Select(e => (int?)e.TicketNumber)
            .MaxAsync(cancellationToken) ?? 0;

        return Math.Max(maxPaymentTicket, maxExpenseTicket) + 1;
    }
}
