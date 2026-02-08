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
}
