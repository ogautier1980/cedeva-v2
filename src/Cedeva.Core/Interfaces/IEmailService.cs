namespace Cedeva.Core.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlContent, string? attachmentPath = null);
    Task SendEmailAsync(IEnumerable<string> to, string subject, string htmlContent, string? attachmentPath = null);
}
