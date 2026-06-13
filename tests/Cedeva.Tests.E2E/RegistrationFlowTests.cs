using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// End-to-end coverage of the public iframe registration: a real anonymous browser filling the
/// form and submitting. Exercises client validation, the new server-side NRN check, the
/// multi-tenancy bypass for the anonymous flow, and the confirmation page.
/// </summary>
[Collection("E2E")]
public class RegistrationFlowTests
{
    private readonly PlaywrightFixture _fx;

    public RegistrationFlowTests(PlaywrightFixture fx) => _fx = fx;

    private async Task<IPage> OpenRegistrationFormAsync()
    {
        var page = await _fx.Browser.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/PublicRegistration/Register?activityId={_fx.ActivityId}");
        return page;
    }

    private static async Task FillFormAsync(IPage page, string childNationalRegisterNumber)
    {
        await page.FillAsync("#ParentFirstName", "Jean");
        await page.FillAsync("#ParentLastName", "Dupont");
        await page.FillAsync("#ParentEmail", "jean.dupont@test.be");
        await page.FillAsync("#ParentPhoneNumber", "0470000000");
        await page.FillAsync("#ParentStreet", "Rue de Test 1");
        await page.FillAsync("#ParentPostalCode", "1000");
        await page.FillAsync("#ParentCity", "Bruxelles");
        await page.FillAsync("#ParentNationalRegisterNumber", "85.06.15-133.80");
        await page.FillAsync("#ChildFirstName", "Marie");
        await page.FillAsync("#ChildLastName", "Dupont");
        await page.FillAsync("#ChildBirthDate", "2016-07-08");
        await page.FillAsync("#ChildNationalRegisterNumber", childNationalRegisterNumber);
    }

    [Fact]
    public async Task FullRegistration_WithValidData_LandsOnConfirmation()
    {
        var page = await OpenRegistrationFormAsync();

        await FillFormAsync(page, childNationalRegisterNumber: "16.07.08-164.10"); // valid checksum
        await page.ClickAsync("button[type=submit]");

        await page.WaitForURLAsync("**/PublicRegistration/Confirmation**");
        page.Url.Should().Contain("/PublicRegistration/Confirmation");
        (await page.InnerTextAsync("body")).Should().Contain("Stage E2E"); // activity summary rendered
    }

    [Fact]
    public async Task Registration_WithInvalidNationalRegisterNumber_IsRejected()
    {
        var page = await OpenRegistrationFormAsync();

        await FillFormAsync(page, childNationalRegisterNumber: "12345678901"); // 11 digits, bad checksum
        await page.ClickAsync("button[type=submit]");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        page.Url.Should().NotContain("/Confirmation", "an invalid NRN must not create a booking");
        var body = await page.InnerTextAsync("body");
        body.Should().Contain("registre national invalide");
    }
}
