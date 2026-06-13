using Cedeva.Website.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace Cedeva.Tests.Helpers;

public class ControllerExtensionsTests
{
    /// <summary>
    /// Minimal concrete Controller used as a test double. RedirectToAction / Redirect /
    /// NotFound are inherited from ControllerBase and need no extra wiring.
    /// </summary>
    private sealed class TestController : Controller
    {
    }

    private static TestController CreateController()
    {
        var httpContext = new DefaultHttpContext();
        var controller = new TestController
        {
            TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>()),
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
                RouteData = new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor()
            }
        };
        return controller;
    }

    /// <summary>
    /// A UrlHelper backed by the request services so IsLocalUrl works as in production.
    /// IsLocalUrl is a pure string check and does not require routing registration.
    /// </summary>
    private static TestController CreateControllerWithUrlHelper()
    {
        var controller = CreateController();
        controller.Url = new UrlHelper(new ActionContext(
            controller.HttpContext,
            controller.ControllerContext.RouteData,
            controller.ControllerContext.ActionDescriptor));
        return controller;
    }

    // ---- Constants -------------------------------------------------------

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        ControllerExtensions.SuccessMessageKey.Should().Be("SuccessMessage");
        ControllerExtensions.ErrorMessageKey.Should().Be("ErrorMessage");
        ControllerExtensions.WarningMessageKey.Should().Be("WarningMessage");
        ControllerExtensions.InfoMessageKey.Should().Be("InfoMessage");
        ControllerExtensions.KeepFiltersKey.Should().Be("KeepFilters");
    }

    // ---- Setters ---------------------------------------------------------

    [Fact]
    public void SetSuccessMessage_StoresUnderSuccessKey()
    {
        var c = CreateController();
        c.SetSuccessMessage("ok");
        c.TempData[ControllerExtensions.SuccessMessageKey].Should().Be("ok");
    }

    [Fact]
    public void SetErrorMessage_StoresUnderErrorKey()
    {
        var c = CreateController();
        c.SetErrorMessage("boom");
        c.TempData[ControllerExtensions.ErrorMessageKey].Should().Be("boom");
    }

    [Fact]
    public void SetWarningMessage_StoresUnderWarningKey()
    {
        var c = CreateController();
        c.SetWarningMessage("careful");
        c.TempData[ControllerExtensions.WarningMessageKey].Should().Be("careful");
    }

    [Fact]
    public void SetInfoMessage_StoresUnderInfoKey()
    {
        var c = CreateController();
        c.SetInfoMessage("fyi");
        c.TempData[ControllerExtensions.InfoMessageKey].Should().Be("fyi");
    }

    // ---- Getters ---------------------------------------------------------

    [Fact]
    public void GetSuccessMessage_ReturnsStoredValue()
    {
        var c = CreateController();
        c.SetSuccessMessage("done");
        c.GetSuccessMessage().Should().Be("done");
    }

    [Fact]
    public void GetErrorMessage_ReturnsStoredValue()
    {
        var c = CreateController();
        c.SetErrorMessage("failed");
        c.GetErrorMessage().Should().Be("failed");
    }

    [Fact]
    public void GetWarningMessage_ReturnsStoredValue()
    {
        var c = CreateController();
        c.SetWarningMessage("warn");
        c.GetWarningMessage().Should().Be("warn");
    }

    [Fact]
    public void GetInfoMessage_ReturnsStoredValue()
    {
        var c = CreateController();
        c.SetInfoMessage("info");
        c.GetInfoMessage().Should().Be("info");
    }

    [Fact]
    public void GetSuccessMessage_WhenAbsent_ReturnsNull()
    {
        CreateController().GetSuccessMessage().Should().BeNull();
    }

    [Fact]
    public void GetErrorMessage_WhenAbsent_ReturnsNull()
    {
        CreateController().GetErrorMessage().Should().BeNull();
    }

    [Fact]
    public void GetWarningMessage_WhenAbsent_ReturnsNull()
    {
        CreateController().GetWarningMessage().Should().BeNull();
    }

    [Fact]
    public void GetInfoMessage_WhenAbsent_ReturnsNull()
    {
        CreateController().GetInfoMessage().Should().BeNull();
    }

    [Fact]
    public void GetSuccessMessage_WhenNonStringStored_ReturnsNull()
    {
        var c = CreateController();
        c.TempData[ControllerExtensions.SuccessMessageKey] = 42;
        c.GetSuccessMessage().Should().BeNull();
    }

    // ---- RedirectToIndexWithSuccess / Error ------------------------------

    [Fact]
    public void RedirectToIndexWithSuccess_SetsMessageAndRedirectsToIndex()
    {
        var c = CreateController();

        var result = c.RedirectToIndexWithSuccess("created");

        result.ActionName.Should().Be("Index");
        result.ControllerName.Should().BeNull();
        c.GetSuccessMessage().Should().Be("created");
    }

    [Fact]
    public void RedirectToIndexWithError_SetsMessageAndRedirectsToIndex()
    {
        var c = CreateController();

        var result = c.RedirectToIndexWithError("nope");

        result.ActionName.Should().Be("Index");
        c.GetErrorMessage().Should().Be("nope");
    }

    // ---- RedirectToActionWithSuccess / Error -----------------------------

    [Fact]
    public void RedirectToActionWithSuccess_SetsMessageAndRedirectsToAction()
    {
        var c = CreateController();

        var result = c.RedirectToActionWithSuccess("Details", "saved", new { id = 7 });

        result.ActionName.Should().Be("Details");
        result.RouteValues.Should().ContainKey("id");
        result.RouteValues!["id"].Should().Be(7);
        c.GetSuccessMessage().Should().Be("saved");
    }

    [Fact]
    public void RedirectToActionWithSuccess_WithoutRouteValues_HasNullRouteValues()
    {
        var c = CreateController();

        var result = c.RedirectToActionWithSuccess("Details", "saved");

        result.ActionName.Should().Be("Details");
        result.RouteValues.Should().BeNull();
        c.GetSuccessMessage().Should().Be("saved");
    }

    [Fact]
    public void RedirectToActionWithError_SetsMessageAndRedirectsToAction()
    {
        var c = CreateController();

        var result = c.RedirectToActionWithError("Edit", "bad", new { id = 3 });

        result.ActionName.Should().Be("Edit");
        result.RouteValues!["id"].Should().Be(3);
        c.GetErrorMessage().Should().Be("bad");
    }

    // ---- NotFoundWithError -----------------------------------------------

    [Fact]
    public void NotFoundWithError_SetsErrorAndReturns404()
    {
        var c = CreateController();

        var result = c.NotFoundWithError("missing");

        result.StatusCode.Should().Be(404);
        c.GetErrorMessage().Should().Be("missing");
    }

    // ---- SetActivityViewData ---------------------------------------------

    [Fact]
    public void SetActivityViewData_StoresIdAndName()
    {
        var c = CreateController();

        c.SetActivityViewData(99, "Summer Camp");

        c.ViewData["ActivityId"].Should().Be(99);
        c.ViewData["ActivityName"].Should().Be("Summer Camp");
    }

    // ---- RedirectToReturnUrlOrAction -------------------------------------

    [Fact]
    public void RedirectToReturnUrlOrAction_WithLocalReturnUrl_RedirectsToUrl()
    {
        var c = CreateControllerWithUrlHelper();

        var result = c.RedirectToReturnUrlOrAction("/Activities/Details/5", "Index");

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("/Activities/Details/5");
    }

    [Fact]
    public void RedirectToReturnUrlOrAction_WithNullReturnUrl_RedirectsToAction()
    {
        var c = CreateControllerWithUrlHelper();

        var result = c.RedirectToReturnUrlOrAction(null, "Index");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
    }

    [Fact]
    public void RedirectToReturnUrlOrAction_WithEmptyReturnUrl_RedirectsToAction()
    {
        var c = CreateControllerWithUrlHelper();

        var result = c.RedirectToReturnUrlOrAction("", "Home", new { id = 1 });

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Home");
        redirect.RouteValues!["id"].Should().Be(1);
    }

    [Fact]
    public void RedirectToReturnUrlOrAction_WithNonLocalReturnUrl_RedirectsToAction()
    {
        var c = CreateControllerWithUrlHelper();

        var result = c.RedirectToReturnUrlOrAction("https://evil.example.com", "Index");

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Index");
    }
}
