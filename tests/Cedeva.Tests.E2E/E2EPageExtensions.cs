using Microsoft.Playwright;

namespace Cedeva.Tests.E2E;

internal static class E2EPageExtensions
{
    /// <summary>
    /// Selects a value on a Choices.js-wrapped &lt;select&gt; (id with or without a leading '#') by
    /// driving the widget the way a user does: open the dropdown and click the option with the given
    /// data-value. This is immune to the timing/sync issues of setting the hidden native select's
    /// value programmatically (Choices re-syncs the native select from its own internal state), and
    /// it correctly fires the change handlers other scripts rely on (e.g. AJAX day loading).
    /// Falls back to a native value-set if the element is a plain (non-Choices) select.
    /// </summary>
    public static async Task SelectChoicesAsync(this IPage page, string id, string value)
    {
        var selectId = id.TrimStart('#');
        var wrapper = page.Locator($"div.choices:has(select#{selectId})");

        if (await wrapper.CountAsync() > 0)
        {
            await wrapper.First.ClickAsync(); // open the dropdown
            await wrapper.First.Locator($".choices__item--choice[data-value='{value}']").ClickAsync();
            return;
        }

        // Plain select: set the value and fire change.
        await page.EvaluateAsync(
            "a => { const e = document.getElementById(a.id); e.value = a.v; e.dispatchEvent(new Event('change', { bubbles: true })); }",
            new { id = selectId, v = value });
    }
}
