using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

/// <summary>
/// Browser test for the postal-code/city autocomplete on an admin form. It exercises the full
/// client stack (jQuery + jQuery UI + /api/AddressApi + unified-address-autocomplete.js) that
/// HTTP-only tests can't see — the exact combination that broke when the address query threw.
/// </summary>
[Collection("E2E")]
public class AddressAutocompleteTests
{
    private readonly PlaywrightFixture _fx;

    public AddressAutocompleteTests(PlaywrightFixture fx) => _fx = fx;

    [Fact]
    public async Task ParentCreate_TypingPostalCode_ShowsMunicipalitySuggestion()
    {
        await using var context = await _fx.NewAuthedContextAsync("Admin", _fx.OrganisationId);
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{_fx.BaseUrl}/Parents/Create");
        response!.Status.Should().Be(200);

        // Type into the combined address field; jQuery UI autocomplete queries the AddressApi.
        await page.Locator("#CombinedAddress").PressSequentiallyAsync("Brux", new LocatorPressSequentiallyOptions { Delay = 80 });

        var suggestion = page.Locator("ul.ui-autocomplete li:has-text(\"Bruxelles\")").First;
        await suggestion.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 7000 });

        (await suggestion.IsVisibleAsync()).Should().BeTrue("the autocomplete should suggest the seeded municipality");
    }
}
