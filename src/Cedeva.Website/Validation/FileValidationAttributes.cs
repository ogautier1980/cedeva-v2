using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Validation;

public class AllowedExtensionsAttribute : ValidationAttribute
{
    private readonly string[] _extensions;

    public AllowedExtensionsAttribute(params string[] extensions)
    {
        _extensions = extensions;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_extensions.Contains(extension))
            {
                return new ValidationResult(GetErrorMessage(validationContext));
            }
        }

        return ValidationResult.Success;
    }

    private string GetErrorMessage(ValidationContext validationContext)
    {
        // Try to get localizer from services
        var localizer = validationContext.GetService(typeof(IStringLocalizer<Localization.SharedResources>))
            as IStringLocalizer<Localization.SharedResources>;

        if (localizer != null)
        {
            return string.Format(
                localizer["Validation.FileExtensionNotAllowed"].Value,
                string.Join(", ", _extensions));
        }

        // Fallback to English if localizer not available
        return $"File extension not allowed. Allowed extensions: {string.Join(", ", _extensions)}";
    }
}

public class MaxFileSizeAttribute : ValidationAttribute
{
    private readonly int _maxFileSize;

    public MaxFileSizeAttribute(int maxFileSize)
    {
        _maxFileSize = maxFileSize;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file)
        {
            if (file.Length > _maxFileSize)
            {
                return new ValidationResult(GetErrorMessage(validationContext));
            }
        }

        return ValidationResult.Success;
    }

    private string GetErrorMessage(ValidationContext validationContext)
    {
        // Fix: Use double division to prevent truncation
        var maxSizeMB = _maxFileSize / (1024.0 * 1024.0);

        // Try to get localizer from services
        var localizer = validationContext.GetService(typeof(IStringLocalizer<Localization.SharedResources>))
            as IStringLocalizer<Localization.SharedResources>;

        if (localizer != null)
        {
            return string.Format(
                localizer["Validation.FileSizeExceeded"].Value,
                maxSizeMB.ToString("0.##"));
        }

        // Fallback to English if localizer not available
        return $"File size exceeds the limit of {maxSizeMB:0.##} MB";
    }
}
