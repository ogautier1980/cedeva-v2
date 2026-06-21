using Cedeva.Core.Entities;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Facade service combining email-related services.
/// Reduces constructor parameter count in controllers.
/// </summary>
public interface IEmailFacadeService
{
    IEmailService Email { get; }
    IEmailRecipientService Recipient { get; }
    IEmailVariableReplacementService VariableReplacement { get; }
    IEmailTemplateService Template { get; }

    /// <summary>
    /// Renders the organisation's default template for the given type (with %variables% resolved from
    /// the booking/organisation) and sends it to the unique recipients. Returns false when there is
    /// no such template or no recipient, so the caller can fall back to a default message.
    /// </summary>
    Task<bool> SendBookingTemplateAsync(
        EmailTemplateType type, int organisationId, IEnumerable<string> recipients,
        Booking booking, Organisation organisation);
}
