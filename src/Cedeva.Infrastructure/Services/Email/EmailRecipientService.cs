using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Services.Email;

public class EmailRecipientService : IEmailRecipientService
{
    private readonly CedevaDbContext _context;
    private readonly ILogger<EmailRecipientService> _logger;

    public EmailRecipientService(
        CedevaDbContext context,
        ILogger<EmailRecipientService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<string>> GetRecipientEmailsAsync(
        int activityId,
        string selectedRecipient,
        int? recipientGroupId = null,
        int? scheduledDayId = null,
        CancellationToken cancellationToken = default)
    {
        List<string> emails;

        try
        {
            // Base query: confirmed bookings for this activity
            var query = _context.Bookings
                .Include(b => b.Child)
                    .ThenInclude(c => c.Parent)
                .Include(b => b.Days)
                .Where(b => b.ActivityId == activityId && b.IsConfirmed);

            // Apply day filter if specified
            if (scheduledDayId.HasValue)
            {
                query = query.Where(b => b.Days.Any(bd =>
                    bd.ActivityDayId == scheduledDayId.Value && bd.IsReserved));
            }

            // Apply recipient type filter
            if (selectedRecipient == "medicalsheetreminder")
            {
                query = query.Where(b => !b.IsMedicalSheet);
            }
            else if (selectedRecipient.StartsWith("group_") && recipientGroupId.HasValue)
            {
                query = query.Where(b => b.GroupId == recipientGroupId);
            }

            emails = await query
                .Where(b => b.Child != null && b.Child.Parent != null && !string.IsNullOrEmpty(b.Child.Parent.Email))
                .Select(b => b.Child.Parent.Email)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} email addresses for activity {ActivityId} with criteria {Criteria}, day filter: {DayId}",
                emails.Count, activityId, selectedRecipient, scheduledDayId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving recipient emails for activity {ActivityId}", activityId);
            throw new InvalidOperationException(
                $"Error retrieving recipient emails for activity {activityId}", ex);
        }

        return emails;
    }
}
