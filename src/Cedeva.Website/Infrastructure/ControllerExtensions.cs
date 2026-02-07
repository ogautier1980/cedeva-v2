using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Cedeva.Website.Infrastructure;

/// <summary>
/// Extension methods for standardizing controller patterns across the application.
/// </summary>
public static class ControllerExtensions
{
    // TempData standardized keys
    public const string SuccessMessageKey = "SuccessMessage";
    public const string ErrorMessageKey = "ErrorMessage";
    public const string WarningMessageKey = "WarningMessage";
    public const string InfoMessageKey = "InfoMessage";
    public const string KeepFiltersKey = "KeepFilters";

    /// <summary>
    /// Sets a success message in TempData with standardized key.
    /// </summary>
    public static void SetSuccessMessage(this Controller controller, string message)
    {
        controller.TempData[SuccessMessageKey] = message;
    }

    /// <summary>
    /// Sets an error message in TempData with standardized key.
    /// </summary>
    public static void SetErrorMessage(this Controller controller, string message)
    {
        controller.TempData[ErrorMessageKey] = message;
    }

    /// <summary>
    /// Sets a warning message in TempData with standardized key.
    /// </summary>
    public static void SetWarningMessage(this Controller controller, string message)
    {
        controller.TempData[WarningMessageKey] = message;
    }

    /// <summary>
    /// Sets an info message in TempData with standardized key.
    /// </summary>
    public static void SetInfoMessage(this Controller controller, string message)
    {
        controller.TempData[InfoMessageKey] = message;
    }

    /// <summary>
    /// Gets a success message from TempData if it exists.
    /// </summary>
    public static string? GetSuccessMessage(this Controller controller)
    {
        return controller.TempData[SuccessMessageKey] as string;
    }

    /// <summary>
    /// Gets an error message from TempData if it exists.
    /// </summary>
    public static string? GetErrorMessage(this Controller controller)
    {
        return controller.TempData[ErrorMessageKey] as string;
    }

    /// <summary>
    /// Gets a warning message from TempData if it exists.
    /// </summary>
    public static string? GetWarningMessage(this Controller controller)
    {
        return controller.TempData[WarningMessageKey] as string;
    }

    /// <summary>
    /// Gets an info message from TempData if it exists.
    /// </summary>
    public static string? GetInfoMessage(this Controller controller)
    {
        return controller.TempData[InfoMessageKey] as string;
    }

    /// <summary>
    /// Redirects to Index action with a success message.
    /// </summary>
    public static RedirectToActionResult RedirectToIndexWithSuccess(this Controller controller, string message)
    {
        controller.SetSuccessMessage(message);
        return controller.RedirectToAction("Index");
    }

    /// <summary>
    /// Redirects to Index action with an error message.
    /// </summary>
    public static RedirectToActionResult RedirectToIndexWithError(this Controller controller, string message)
    {
        controller.SetErrorMessage(message);
        return controller.RedirectToAction("Index");
    }

    /// <summary>
    /// Redirects to specified action with a success message.
    /// </summary>
    public static RedirectToActionResult RedirectToActionWithSuccess(this Controller controller, string actionName, string message, object? routeValues = null)
    {
        controller.SetSuccessMessage(message);
        return controller.RedirectToAction(actionName, routeValues);
    }

    /// <summary>
    /// Redirects to specified action with an error message.
    /// </summary>
    public static RedirectToActionResult RedirectToActionWithError(this Controller controller, string actionName, string message, object? routeValues = null)
    {
        controller.SetErrorMessage(message);
        return controller.RedirectToAction(actionName, routeValues);
    }

    /// <summary>
    /// Returns NotFound with an error message.
    /// </summary>
    public static NotFoundResult NotFoundWithError(this Controller controller, string message)
    {
        controller.SetErrorMessage(message);
        return controller.NotFound();
    }

    /// <summary>
    /// Sets activity information in ViewData for breadcrumbs and navigation.
    /// </summary>
    public static void SetActivityViewData(this Controller controller, int activityId, string activityName)
    {
        controller.ViewData["ActivityId"] = activityId;
        controller.ViewData["ActivityName"] = activityName;
    }

    /// <summary>
    /// Redirects to the returnUrl if it's local and not empty, otherwise redirects to the specified action.
    /// </summary>
    public static IActionResult RedirectToReturnUrlOrAction(this Controller controller, string? returnUrl, string actionName, object? routeValues = null)
    {
        if (!string.IsNullOrEmpty(returnUrl) && controller.Url.IsLocalUrl(returnUrl))
        {
            return controller.Redirect(returnUrl);
        }
        return controller.RedirectToAction(actionName, routeValues);
    }
}
