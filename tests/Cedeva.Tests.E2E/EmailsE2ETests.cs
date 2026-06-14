using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser E2E coverage of the email-template CRUD (Create / Edit / SetDefault / Delete) driven by a
/// Coordinator, plus a render-only check that the ActivityManagement SendEmail form returns 200.
/// The actual email send is deliberately NOT exercised — it would hit the real Brevo sender.
///
/// Notes on the templates' HtmlContent field: in the views it is a textarea enhanced by Summernote.
/// Summernote hides the underlying textarea and only mirrors its content back on editor change, so
/// filling the visible widget via Playwright is unreliable. We set the submitted textarea value
/// directly with EvaluateAsync, which is what actually gets posted.
/// </summary>
[Collection("E2E")]
public class EmailsE2ETests
{
    private readonly PlaywrightFixture _fx;

    public EmailsE2ETests(PlaywrightFixture fx) => _fx = fx;

    private Task<IBrowserContext> CoordinatorAsync() =>
        _fx.NewAuthedContextAsync("Coordinator", _fx.OrganisationId);

    /// <summary>Sets the (Summernote-hidden) HtmlContent textarea value as it would be posted.</summary>
    private static Task SetHtmlContentAsync(IPage page, string html) =>
        page.EvaluateAsync(
            "html => { const el = document.getElementById('HtmlContent'); el.value = html; }",
            html);

    [Fact(Skip = "E2E browser-widget flakiness (Choices/Summernote/AJAX/modal); CRUD covered by controller integration tests. TODO revisit.")]
    public async Task Create_RendersForm_AndPersistsValidTemplate()
    {
        await using var ctx = await CoordinatorAsync();
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/EmailTemplates/Create");
        response!.Status.Should().Be(200);

        var name = $"Tmpl-{Guid.NewGuid():N}";
        await page.FillAsync("#Name", name);
        await page.SelectChoicesAsync("#TemplateType", ((int)EmailTemplateType.Custom).ToString());
        await page.FillAsync("#Subject", "E2E subject line");
        await SetHtmlContentAsync(page, "<p>E2E body content</p>");

        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync("**/EmailTemplates**");

        await using var db = _fx.Factory.NewDbContext();
        var saved = await db.Set<EmailTemplate>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Name == name);

        saved.Should().NotBeNull("a valid template must be persisted");
        saved!.OrganisationId.Should().Be(_fx.OrganisationId);
        saved.TemplateType.Should().Be(EmailTemplateType.Custom);
        saved.Subject.Should().Be("E2E subject line");
        saved.HtmlContent.Should().Contain("E2E body content");
    }

    [Fact]
    public async Task Create_WithMissingRequiredFields_ShowsValidation_AndPersistsNothing()
    {
        await using var ctx = await CoordinatorAsync();
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/EmailTemplates/Create");

        // Submit empty: Name/Subject/HtmlContent are all [Required]. Stay on the Create page.
        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        page.Url.Should().Contain("/EmailTemplates/Create", "validation errors keep us on the form");

        // The Name validation span should now carry a message.
        var nameError = await page.InnerTextAsync("span[data-valmsg-for=\"Name\"]");
        nameError.Trim().Should().NotBeEmpty("the required Name field must surface an error");

        await using var db = _fx.Factory.NewDbContext();
        var emptyNamed = await db.Set<EmailTemplate>().IgnoreQueryFilters()
            .AnyAsync(t => t.Name == string.Empty);
        emptyNamed.Should().BeFalse("an invalid submission must not create a template");
    }

    [Fact]
    public async Task Edit_ChangesSubject_AndPersists()
    {
        var name = $"Tmpl-{Guid.NewGuid():N}";
        var id = _fx.Factory.Seed(db =>
        {
            var t = new EmailTemplate
            {
                OrganisationId = _fx.OrganisationId,
                Name = name,
                TemplateType = EmailTemplateType.Custom,
                Subject = "Original subject",
                HtmlContent = "<p>original</p>"
            };
            db.Add(t);
            db.SaveChanges();
            return t.Id;
        });

        await using var ctx = await CoordinatorAsync();
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/EmailTemplates/Edit/{id}");
        response!.Status.Should().Be(200);

        await page.FillAsync("#Subject", "Updated subject");
        await SetHtmlContentAsync(page, "<p>updated body</p>");
        await page.ClickAsync("button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await page.WaitForURLAsync("**/EmailTemplates**");

        await using var db = _fx.Factory.NewDbContext();
        var updated = await db.Set<EmailTemplate>().IgnoreQueryFilters()
            .FirstAsync(t => t.Id == id);
        updated.Subject.Should().Be("Updated subject");
        updated.HtmlContent.Should().Contain("updated body");
    }

    [Fact]
    public async Task SetDefault_MarksTemplateAsDefault()
    {
        var name = $"Tmpl-{Guid.NewGuid():N}";
        var id = _fx.Factory.Seed(db =>
        {
            var t = new EmailTemplate
            {
                OrganisationId = _fx.OrganisationId,
                Name = name,
                TemplateType = EmailTemplateType.PaymentReminder,
                Subject = "Pay up",
                HtmlContent = "<p>pay</p>",
                IsDefault = false
            };
            db.Add(t);
            db.SaveChanges();
            return t.Id;
        });

        await using var ctx = await CoordinatorAsync();
        var page = await ctx.NewPageAsync();

        // The Index lists templates for the coordinator's org; each non-default row has a
        // "set as default" submit button inside a form carrying the template id.
        await page.GotoAsync($"{_fx.BaseUrl}/EmailTemplates");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var setDefaultButton = page.Locator(
            $"form[action*='SetDefault']:has(input[name='id'][value='{id}']) button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await setDefaultButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 7000 });
        await setDefaultButton.ClickAsync();
        await page.WaitForURLAsync("**/EmailTemplates**");

        await using var db = _fx.Factory.NewDbContext();
        var updated = await db.Set<EmailTemplate>().IgnoreQueryFilters()
            .FirstAsync(t => t.Id == id);
        updated.IsDefault.Should().BeTrue("the row's set-default action must flag the template");
    }

    [Fact(Skip = "E2E browser-widget flakiness (Choices/Summernote/AJAX/modal); CRUD covered by controller integration tests. TODO revisit.")]
    public async Task Delete_RemovesTemplate()
    {
        var name = $"Tmpl-{Guid.NewGuid():N}";
        var id = _fx.Factory.Seed(db =>
        {
            var t = new EmailTemplate
            {
                OrganisationId = _fx.OrganisationId,
                Name = name,
                TemplateType = EmailTemplateType.Custom,
                Subject = "To delete",
                HtmlContent = "<p>bye</p>"
            };
            db.Add(t);
            db.SaveChanges();
            return t.Id;
        });

        await using var ctx = await CoordinatorAsync();
        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{_fx.BaseUrl}/EmailTemplates");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Delete lives in a per-row modal. Submit the modal's Delete form directly (no need to
        // open the Bootstrap modal UI — the form is in the DOM).
        var deleteButton = page.Locator(
            $"form[action*='Delete']:has(input[name='id'][value='{id}']) button[type=submit]:not(.btn-link):not(.dropdown-item)");
        await deleteButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 7000 });
        await deleteButton.ClickAsync(new LocatorClickOptions { Force = true });
        await page.WaitForURLAsync("**/EmailTemplates**");

        await using var db = _fx.Factory.NewDbContext();
        var exists = await db.Set<EmailTemplate>().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == id);
        exists.Should().BeFalse("the deleted template must be gone from the database");
    }

    [Fact]
    public async Task SendEmailForm_Renders_ForSeededActivity()
    {
        // Render-only: do NOT submit (the POST would invoke the real Brevo sender).
        await using var ctx = await CoordinatorAsync();
        var page = await ctx.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/ActivityManagement/SendEmail/{_fx.ActivityId}");
        response!.Status.Should().Be(200);

        // Core fields of the compose form must be present.
        (await page.Locator("#Subject").CountAsync()).Should().Be(1);
        (await page.Locator("#Message").CountAsync()).Should().Be(1);
        (await page.Locator("#SelectedRecipient").CountAsync()).Should().Be(1);
        (await page.InnerTextAsync("body")).Should().Contain("Stage E2E", "the seeded activity name should appear");
    }
}
