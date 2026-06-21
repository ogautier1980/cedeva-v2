namespace Cedeva.Core.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends a transactional email to a single recipient
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlContent">HTML content of the email</param>
    /// <param name="attachmentPath">Optional absolute file path for attachment</param>
    /// <returns>Task representing the async operation</returns>
    Task SendEmailAsync(string to, string subject, string htmlContent, string? attachmentPath = null);

    /// <summary>
    /// Sends a transactional email to multiple recipients
    /// </summary>
    /// <param name="to">List of recipient email addresses</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlContent">HTML content of the email</param>
    /// <param name="attachmentPath">Optional absolute file path for attachment</param>
    /// <returns>Task representing the async operation</returns>
    Task SendEmailAsync(IEnumerable<string> to, string subject, string htmlContent, string? attachmentPath = null);

    /// <summary>
    /// Sends a welcome email to a newly registered user
    /// </summary>
    /// <param name="userEmail">User email address</param>
    /// <param name="userName">User full name</param>
    /// <param name="organisationName">Organisation name</param>
    /// <returns>Task representing the async operation</returns>
    Task SendWelcomeEmailAsync(string userEmail, string userName, string organisationName);
}
