using Cedeva.Core.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace Cedeva.Website.Infrastructure;

/// <summary>
/// Binds <c>decimal</c>/<c>decimal?</c> form fields via <see cref="DecimalInputHelper"/> instead of
/// the framework default (which parses under the fr-BE request culture and silently misreads a
/// period-typed amount like "38.50" as 3850 — "." is fr-BE's thousands separator, not its decimal one).
/// </summary>
public class DecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            // Mirror the framework's SimpleTypeModelBinder: a blank value binds to null, which is
            // only acceptable for decimal? — a plain decimal reports the same "must not be null"
            // error the default binder would have raised for a missing required field.
            if (bindingContext.ModelMetadata.IsReferenceOrNullableType)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(
                    bindingContext.ModelName,
                    bindingContext.ModelMetadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(valueProviderResult.ToString()));
            }

            return Task.CompletedTask;
        }

        if (DecimalInputHelper.TryParse(value, out var result))
        {
            bindingContext.Result = ModelBindingResult.Success(result);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                bindingContext.ModelMetadata.ModelBindingMessageProvider.ValueMustBeANumberAccessor(valueProviderResult.FirstValue ?? string.Empty));
        }

        return Task.CompletedTask;
    }
}

public class DecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.Metadata.UnderlyingOrModelType;
        if (modelType == typeof(decimal))
            return new DecimalModelBinder();

        return null;
    }
}
