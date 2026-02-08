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
}
