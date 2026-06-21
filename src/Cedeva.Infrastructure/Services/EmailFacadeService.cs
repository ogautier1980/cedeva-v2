using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;

namespace Cedeva.Infrastructure.Services;

/// <summary>
/// Facade service combining email-related services.
/// </summary>
public class EmailFacadeService : IEmailFacadeService
{
    public IEmailService Email { get; }
    public IEmailRecipientService Recipient { get; }
    public IEmailVariableReplacementService VariableReplacement { get; }
    public IEmailTemplateService Template { get; }

    public EmailFacadeService(
        IEmailService email,
        IEmailRecipientService recipient,
        IEmailVariableReplacementService variableReplacement,
        IEmailTemplateService template)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
        VariableReplacement = variableReplacement ?? throw new ArgumentNullException(nameof(variableReplacement));
        Template = template ?? throw new ArgumentNullException(nameof(template));
    }

    /// <summary>
    /// Renders the organisation's default template for <paramref name="type"/> (subject + body, with
    /// %variables% resolved from the booking/organisation) and sends it to the unique recipients.
    /// Returns false when no template exists or there is no recipient, so the caller can fall back.
    /// </summary>
    public async Task<bool> SendBookingTemplateAsync(
        EmailTemplateType type, int organisationId, IEnumerable<string> recipients,
        Booking booking, Organisation organisation)
    {
        var template = await Template.GetDefaultTemplateAsync(type, organisationId);
        if (template == null)
            return false;

        var to = recipients.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList();
        if (to.Count == 0)
            return false;

        var subject = VariableReplacement.ReplaceVariables(template.Subject, booking, organisation);
        var html = VariableReplacement.ReplaceVariables(template.HtmlContent, booking, organisation);
        await Email.SendEmailAsync(to, subject, html);
        return true;
    }
}
