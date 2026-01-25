using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Helpers;

public static class EnumLocalizer
{
    public static string GetLocalizedEnum<TEnum>(this IStringLocalizer localizer, TEnum enumValue) where TEnum : Enum
    {
        var enumTypeName = typeof(TEnum).Name;
        var enumValueName = enumValue.ToString();
        var key = $"Enum.{enumTypeName}.{enumValueName}";

        var localizedString = localizer[key];

        // If localization key not found, return the enum value as string
        return localizedString.ResourceNotFound ? enumValueName : localizedString.Value;
    }
}
