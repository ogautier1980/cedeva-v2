using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Services;

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
        CancellationToken cancellationToken = default)
    {
        var emails = new List<string>();

        try
        {
            if (selectedRecipient == "allparents")
            {
                // All parents with children registered to this activity
                emails = await _context.Bookings
                    .Where(b => b.ActivityId == activityId && b.IsConfirmed)
                    .Select(b => b.Child.Parent.Email)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }
            else if (selectedRecipient == "medicalsheetreminder")
            {
                // Parents of children without medical sheet
                emails = await _context.Bookings
                    .Where(b => b.ActivityId == activityId && b.IsConfirmed && !b.IsMedicalSheet)
                    .Select(b => b.Child.Parent.Email)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }
            else if (selectedRecipient.StartsWith("group_") && recipientGroupId.HasValue)
            {
                // Parents of children in specific group
                emails = await _context.Bookings
                    .Where(b => b.ActivityId == activityId && b.IsConfirmed && b.GroupId == recipientGroupId)
                    .Select(b => b.Child.Parent.Email)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }

            _logger.LogInformation(
                "Retrieved {Count} email addresses for activity {ActivityId} with criteria {Criteria}",
                emails.Count, activityId, selectedRecipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving recipient emails for activity {ActivityId}", activityId);
            throw;
        }

        return emails;
    }
}
