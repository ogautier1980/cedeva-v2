using Microsoft.AspNetCore.Localization;

namespace Cedeva.Website.Infrastructure;

/// <summary>
/// The interface languages (FR/NL/EN) and the culture cookie that selects them. Used by the user's
/// profile (saved preference, re-applied at login) and by Home/SetLanguage.
/// </summary>
public static class UiLanguage
{
    public const string Default = "fr";

    public static readonly IReadOnlyList<string> Supported = ["fr", "nl", "en"];

    public static bool IsSupported(string? language) => language != null && Supported.Contains(language);

    /// <summary>
    /// Writes the culture cookie: Belgian formatting (fr-BE, nl-BE, en-BE — the SupportedCultures
    /// configured in Program.cs) and the neutral UI culture that picks the resx file.
    /// </summary>
    public static void WriteCookie(HttpResponse response, string language)
    {
        response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture: $"{language}-BE", uiCulture: language)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });
    }
}
