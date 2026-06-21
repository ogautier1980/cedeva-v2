using System.Net;
using Cedeva.Core.DTOs;
using Cedeva.Infrastructure.Configuration;
using Cedeva.Infrastructure.Services.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Cedeva.Tests.Services.Email;

public class BrevoEmailServiceTests
{
    // ---- HttpMessageHandler stub that records the last request and returns a configured response ----
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public StubHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "{\"messageId\":\"ok\"}")
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body),
            };
        }
    }

    private static IOptions<BrevoOptions> Options(
        string senderEmail = "sender@cedeva.be",
        string senderName = "Cedeva") =>
        Microsoft.Extensions.Options.Options.Create(new BrevoOptions
        {
            ApiBaseUrl = "https://api.brevo.com",
            ApiKey = "key",
            SenderEmail = senderEmail,
            SenderName = senderName,
        });

    /// <summary>
    /// Builds the service wired to an IHttpClientFactory that returns an HttpClient backed by the
    /// supplied stub handler. A BaseAddress is required because the service POSTs to a relative path.
    /// </summary>
    private static BrevoEmailService BuildService(
        StubHandler handler,
        IOptions<BrevoOptions>? options = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com") };
        factory.CreateClient("BrevoClient").Returns(client);

        return new BrevoEmailService(
            options ?? Options(),
            factory,
            NullLogger<BrevoEmailService>.Instance);
    }

    // -------------------- Constructor validation --------------------

    [Fact]
    public void Constructor_throws_when_sender_email_missing()
    {
        var act = () => BuildService(new StubHandler(), Options(senderEmail: "  "));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*sender email*");
    }

    [Fact]
    public void Constructor_throws_when_sender_name_missing()
    {
        var act = () => BuildService(new StubHandler(), Options(senderName: ""));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*sender name*");
    }

    // -------------------- SendEmailAsync (single + collection) --------------------

    [Fact]
    public async Task SendEmailAsync_posts_to_brevo_smtp_endpoint_on_success()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        await service.SendEmailAsync("parent@example.com", "Subject", "<p>Hi</p>");

        handler.CallCount.Should().Be(1);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/v3/smtp/email");
    }

    [Fact]
    public async Task SendEmailAsync_includes_sender_recipient_subject_and_html_in_payload()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        await service.SendEmailAsync("parent@example.com", "My Subject", "<p>Body</p>");

        var body = handler.LastRequestBody!;
        body.Should().Contain("sender@cedeva.be");
        body.Should().Contain("Cedeva");
        body.Should().Contain("parent@example.com");
        body.Should().Contain("My Subject");
        body.Should().Contain("\\u003Cp\\u003EBody"); // html is JSON-escaped
    }

    [Fact]
    public async Task SendEmailAsync_trims_recipients_and_drops_blank_entries()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        await service.SendEmailAsync(
            new[] { "  a@example.com  ", "", "   ", "b@example.com" },
            "Subject", "<p>Hi</p>");

        handler.CallCount.Should().Be(1);
        var body = handler.LastRequestBody!;
        body.Should().Contain("a@example.com");
        body.Should().Contain("b@example.com");
        // trimmed: no leading/trailing whitespace variant present
        body.Should().NotContain("  a@example.com  ");
    }

    [Fact]
    public async Task SendEmailAsync_short_circuits_when_single_recipient_is_whitespace()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        await service.SendEmailAsync("   ", "Subject", "<p>Hi</p>");

        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task SendEmailAsync_short_circuits_when_recipient_list_is_empty()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        await service.SendEmailAsync(Array.Empty<string>(), "Subject", "<p>Hi</p>");

        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task SendEmailAsync_short_circuits_when_all_recipients_blank()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        await service.SendEmailAsync(new[] { "", "  ", null! }, "Subject", "<p>Hi</p>");

        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task SendEmailAsync_throws_InvalidOperationException_on_non_success_status()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, "{\"error\":\"bad\"}");
        var service = BuildService(handler);

        var act = async () => await service.SendEmailAsync("parent@example.com", "Subject", "<p>Hi</p>");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Failed to send email*");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendEmailAsync_wraps_transport_exception_in_InvalidOperationException()
    {
        // Handler that throws to simulate a network failure.
        var service = BuildServiceWithThrowingHandler();

        var act = async () => await service.SendEmailAsync("parent@example.com", "Subject", "<p>Hi</p>");

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("*Failed to send email*");
        ex.And.InnerException.Should().NotBeNull();
    }

    private static BrevoEmailService BuildServiceWithThrowingHandler()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var client = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("https://api.brevo.com") };
        factory.CreateClient("BrevoClient").Returns(client);
        return new BrevoEmailService(Options(), factory, NullLogger<BrevoEmailService>.Instance);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("boom");
    }

    // -------------------- Attachment handling --------------------

    [Fact]
    public async Task SendEmailAsync_includes_attachment_when_file_exists()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        var path = Path.Combine(Path.GetTempPath(), $"brevo-test-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "hello world");
        try
        {
            await service.SendEmailAsync("parent@example.com", "Subject", "<p>Hi</p>", path);

            var body = handler.LastRequestBody!;
            body.Should().Contain("attachment");
            body.Should().Contain(Path.GetFileName(path));
            body.Should().Contain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("hello world")));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SendEmailAsync_sends_without_attachment_when_file_missing()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        var missing = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.txt");

        await service.SendEmailAsync("parent@example.com", "Subject", "<p>Hi</p>", missing);

        handler.CallCount.Should().Be(1);
        handler.LastRequestBody!.Should().NotContain("attachment");
    }

    // -------------------- SendBookingConfirmationEmailAsync (simple overload) --------------------

    [Fact]
    public async Task SendBookingConfirmationEmailAsync_simple_builds_html_and_delegates_to_send()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        await service.SendBookingConfirmationEmailAsync(
            parentEmail: "parent@example.com",
            parentName: "Jean Dupont",
            childName: "Chloe Enfant",
            activityName: "Stage Multisports",
            startDate: new DateTime(2026, 7, 1),
            endDate: new DateTime(2026, 7, 5));

        handler.CallCount.Should().Be(1);
        var body = handler.LastRequestBody!;
        body.Should().Contain("parent@example.com");
        // The apostrophe in "d'inscription" is JSON-escaped, so assert the stable substring.
        body.Should().Contain("inscription - Stage Multisports");
        body.Should().Contain("Jean Dupont");
        body.Should().Contain("Chloe Enfant");
        body.Should().Contain("Stage Multisports");
        // Dates are rendered with dd/MM/yyyy format string; separator is culture-dependent.
        body.Should().Contain("2026");
    }

    // -------------------- SendWelcomeEmailAsync --------------------

    [Fact]
    public async Task SendWelcomeEmailAsync_builds_html_and_delegates_to_send()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = BuildService(handler);

        await service.SendWelcomeEmailAsync(
            userEmail: "user@example.com",
            userName: "Paul Coordinator",
            organisationName: "Centre de Liege");

        handler.CallCount.Should().Be(1);
        var body = handler.LastRequestBody!;
        body.Should().Contain("user@example.com");
        body.Should().Contain("Bienvenue sur Cedeva");
        body.Should().Contain("Paul Coordinator");
        body.Should().Contain("Centre de Liege");
    }

    [Fact]
    public async Task Higher_level_helpers_propagate_send_failure()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, "boom");
        var service = BuildService(handler);

        var act = async () => await service.SendWelcomeEmailAsync("user@example.com", "Paul", "Org");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Failed to send email*");
    }
}
