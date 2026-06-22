using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cedeva.Infrastructure.Services.Email;

/// <inheritdoc cref="IActivityEmailService"/>
public class ActivityEmailService : IActivityEmailService
{
    private readonly CedevaDbContext _context;
    private readonly IEmailFacadeService _email;
    private readonly IContactDirectoryService _contactDirectory;
    private readonly ILogger<ActivityEmailService> _logger;

    public ActivityEmailService(
        CedevaDbContext context,
        IEmailFacadeService email,
        IContactDirectoryService contactDirectory,
        ILogger<ActivityEmailService> logger)
    {
        _context = context;
        _email = email;
        _contactDirectory = contactDirectory;
        _logger = logger;
    }

    public async Task<ActivityEmailResult> SendAsync(ActivityEmailRequest request, CancellationToken ct = default)
    {
        var organisationId = await _context.Activities
            .Where(a => a.Id == request.ActivityId)
            .Select(a => a.OrganisationId)
            .FirstOrDefaultAsync(ct);
        var organisation = await _context.Organisations.FirstOrDefaultAsync(o => o.Id == organisationId, ct);

        _logger.LogInformation("Starting email sending process for activity {ActivityId}, recipient: {Recipient}",
            request.ActivityId, request.SelectedRecipient);

        // Contact recipients (ad-hoc selection or a saved group): send the message as-is to the chosen
        // contact emails (no per-child variable replacement — these recipients have no booking context).
        var isContactGroup = request.SelectedRecipient?.StartsWith(EmailRecipientKeys.ContactGroupPrefix) == true;
        if (request.SelectedRecipient == EmailRecipientKeys.CustomContacts || isContactGroup)
        {
            var customEmails = await ResolveContactEmailsAsync(request, organisationId, isContactGroup, ct);
            if (customEmails.Count == 0)
                return new ActivityEmailResult(ActivityEmailOutcome.NoContactsSelected, 0);

            foreach (var emailAddress in customEmails)
                await _email.Email.SendEmailAsync(new List<string> { emailAddress }, request.Subject, request.Message, request.AttachmentFilePath);

            await LogSentEmailAsync(request, null, customEmails, ct);
            return new ActivityEmailResult(ActivityEmailOutcome.Sent, customEmails.Count);
        }

        var recipientGroupId = ExtractRecipientGroupId(request.SelectedRecipient);
        var sentCount = request.SendSeparateEmailPerChild
            ? await SendPerChildAsync(request, recipientGroupId, organisation!, ct)
            : await SendPerParentAsync(request, recipientGroupId, ct);

        if (sentCount == 0)
            return new ActivityEmailResult(ActivityEmailOutcome.NoRecipients, 0);

        var allEmails = await _email.Recipient.GetRecipientEmailsAsync(
            request.ActivityId, request.SelectedRecipient!, recipientGroupId, request.SelectedDayId, ct);
        await LogSentEmailAsync(request, recipientGroupId, allEmails, ct);

        return new ActivityEmailResult(ActivityEmailOutcome.Sent, sentCount);
    }

    /// <summary>
    /// Resolves the ad-hoc / saved-group recipient emails, restricted to addresses that actually belong
    /// to this organisation's contacts (so the form can't be used to email arbitrary addresses).
    /// </summary>
    private async Task<List<string>> ResolveContactEmailsAsync(
        ActivityEmailRequest request, int organisationId, bool isContactGroup, CancellationToken ct)
    {
        var allowed = (await _contactDirectory.GetSelectableContactsAsync(organisationId, ct))
            .Select(c => c.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> requestedEmails;
        if (isContactGroup &&
            int.TryParse(request.SelectedRecipient!.Substring(EmailRecipientKeys.ContactGroupPrefix.Length), out var contactGroupId))
        {
            requestedEmails = await _context.ContactGroupMembers
                .Where(m => m.ContactGroupId == contactGroupId && m.ContactGroup.OrganisationId == organisationId)
                .Select(m => m.Email)
                .ToListAsync(ct);
        }
        else
        {
            requestedEmails = request.SelectedContactEmails;
        }

        return requestedEmails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Where(e => allowed.Contains(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>One personalized email per confirmed booking (variables resolved per booking).</summary>
    private async Task<int> SendPerChildAsync(ActivityEmailRequest request, int? recipientGroupId, Organisation organisation, CancellationToken ct)
    {
        var bookings = await GetFilteredBookingsAsync(request, recipientGroupId, ct);

        var count = 0;
        foreach (var booking in bookings)
        {
            var subject = _email.VariableReplacement.ReplaceVariables(request.Subject, booking, organisation);
            var message = _email.VariableReplacement.ReplaceVariables(request.Message, booking, organisation);
            await _email.Email.SendEmailAsync(new List<string> { booking.Child.Parent.Email }, subject, message, request.AttachmentFilePath);
            count++;
        }

        return count;
    }

    /// <summary>One email per unique parent email (message sent as-is).</summary>
    private async Task<int> SendPerParentAsync(ActivityEmailRequest request, int? recipientGroupId, CancellationToken ct)
    {
        var recipientEmails = await _email.Recipient.GetRecipientEmailsAsync(
            request.ActivityId, request.SelectedRecipient!, recipientGroupId, request.SelectedDayId, ct);

        foreach (var emailAddress in recipientEmails)
            await _email.Email.SendEmailAsync(new List<string> { emailAddress }, request.Subject, request.Message, request.AttachmentFilePath);

        return recipientEmails.Count;
    }

    /// <summary>Confirmed bookings matching the recipient/day filters, with graph loaded for variables.</summary>
    private async Task<List<Booking>> GetFilteredBookingsAsync(ActivityEmailRequest request, int? recipientGroupId, CancellationToken ct)
    {
        var query = _context.Bookings
            .Include(b => b.Child).ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .Include(b => b.Group)
            .Include(b => b.Days)
            .Where(b => b.ActivityId == request.ActivityId && b.IsConfirmed);

        if (request.SelectedDayId.HasValue)
            query = query.Where(b => b.Days.Any(bd => bd.ActivityDayId == request.SelectedDayId.Value && bd.IsReserved));

        var selectedRecipient = request.SelectedRecipient;
        if (selectedRecipient == EmailRecipientKeys.MedicalSheetReminder)
        {
            query = query.Where(b => !b.IsMedicalSheet);
        }
        else if (selectedRecipient == EmailRecipientKeys.UnpaidParents)
        {
            query = query.Where(b => b.PaidAmount < b.TotalAmount);
        }
        else if (!string.IsNullOrEmpty(selectedRecipient) && selectedRecipient.StartsWith(EmailRecipientKeys.GroupPrefix) && recipientGroupId.HasValue)
        {
            query = query.Where(b => b.GroupId == recipientGroupId);
        }
        else if (!string.IsNullOrEmpty(selectedRecipient) && selectedRecipient.StartsWith(EmailRecipientKeys.ExcursionPrefix))
        {
            if (int.TryParse(selectedRecipient.Substring(EmailRecipientKeys.ExcursionPrefix.Length), out var excursionId))
            {
                var registeredBookingIds = await _context.ExcursionRegistrations
                    .Where(er => er.ExcursionId == excursionId)
                    .Select(er => er.BookingId)
                    .ToListAsync(ct);
                query = query.Where(b => registeredBookingIds.Contains(b.Id));
            }
        }

        return await query.ToListAsync(ct);
    }

    private async Task LogSentEmailAsync(ActivityEmailRequest request, int? recipientGroupId, IEnumerable<string> recipientEmails, CancellationToken ct)
    {
        _context.EmailsSent.Add(new EmailSent
        {
            ActivityId = request.ActivityId,
            RecipientType = DetermineRecipientType(request.SelectedRecipient),
            RecipientGroupId = recipientGroupId,
            ScheduledDayId = request.SelectedDayId,
            RecipientEmails = string.Join("; ", recipientEmails),
            Subject = request.Subject,
            Message = request.Message,
            SendSeparateEmailPerChild = request.SendSeparateEmailPerChild,
            AttachmentFileName = request.AttachmentFileName,
            AttachmentFilePath = request.AttachmentFilePath,
            SentDate = DateTime.Now
        });
        await _context.SaveChangesAsync(ct);
    }

    private static EmailRecipient DetermineRecipientType(string? selectedRecipient) => selectedRecipient switch
    {
        EmailRecipientKeys.AllParents => EmailRecipient.AllParents,
        EmailRecipientKeys.MedicalSheetReminder => EmailRecipient.MedicalSheetReminder,
        EmailRecipientKeys.CustomContacts => EmailRecipient.CustomContacts,
        var r when r != null && r.StartsWith(EmailRecipientKeys.ContactGroupPrefix) => EmailRecipient.CustomContacts,
        var r when r != null && r.StartsWith(EmailRecipientKeys.GroupPrefix) => EmailRecipient.ActivityGroup,
        _ => EmailRecipient.AllParents
    };

    private static int? ExtractRecipientGroupId(string? selectedRecipient) =>
        !string.IsNullOrEmpty(selectedRecipient)
        && selectedRecipient.StartsWith(EmailRecipientKeys.GroupPrefix)
        && int.TryParse(selectedRecipient.Substring(EmailRecipientKeys.GroupPrefix.Length), out var groupId)
            ? groupId
            : null;
}
