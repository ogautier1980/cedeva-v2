using Cedeva.Core.Interfaces;

namespace Cedeva.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="IEmailService"/> that records every send instead of calling Brevo, so the
/// email-sending controller pipeline can be exercised in tests without hitting the network. Register
/// it as a singleton via <see cref="CedevaWebApplicationFactory.ConfigureExtraTestServices"/> and
/// read <see cref="Sent"/> after the request.
/// </summary>
public sealed class FakeEmailService : IEmailService
{
    public sealed record Message(IReadOnlyList<string> To, string Subject, string Html, string? AttachmentPath);

    public List<Message> Sent { get; } = new();

    public Task SendEmailAsync(string to, string subject, string htmlContent, string? attachmentPath = null)
    {
        Sent.Add(new Message(new[] { to }, subject, htmlContent, attachmentPath));
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(IEnumerable<string> to, string subject, string htmlContent, string? attachmentPath = null)
    {
        Sent.Add(new Message(to.ToList(), subject, htmlContent, attachmentPath));
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string userEmail, string userName, string organisationName)
    {
        Sent.Add(new Message(new[] { userEmail }, $"Welcome: {organisationName}", userName, null));
        return Task.CompletedTask;
    }
}
