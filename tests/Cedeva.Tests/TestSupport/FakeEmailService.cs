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
    /// <param name="AttachmentBytes">The attachment's content, read at send time (callers may delete
    /// a temporary attachment file right after sending).</param>
    public sealed record Message(IReadOnlyList<string> To, string Subject, string Html, string? AttachmentPath,
        byte[]? AttachmentBytes = null);

    public List<Message> Sent { get; } = new();

    public Task SendEmailAsync(string to, string subject, string htmlContent, string? attachmentPath = null)
    {
        Sent.Add(new Message(new[] { to }, subject, htmlContent, attachmentPath, ReadAttachment(attachmentPath)));
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(IEnumerable<string> to, string subject, string htmlContent, string? attachmentPath = null)
    {
        Sent.Add(new Message(to.ToList(), subject, htmlContent, attachmentPath, ReadAttachment(attachmentPath)));
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string userEmail, string userName, string organisationName)
    {
        Sent.Add(new Message(new[] { userEmail }, $"Welcome: {organisationName}", userName, null));
        return Task.CompletedTask;
    }

    private static byte[]? ReadAttachment(string? path) =>
        !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllBytes(path) : null;
}
