using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Account.ViewModels;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Account;

public class AccountController : Controller
{
    private const string ViewDataReturnUrl = "ReturnUrl";

    private readonly SignInManager<CedevaUser> _signInManager;
    private readonly UserManager<CedevaUser> _userManager;
    private readonly IRepository<Organisation> _organisationRepository;
    private readonly IEmailService _emailService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<CedevaUser> signInManager,
        UserManager<CedevaUser> userManager,
        IRepository<Organisation> organisationRepository,
        IEmailService emailService,
        IStringLocalizer<SharedResources> localizer,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _organisationRepository = organisationRepository;
        _emailService = emailService;
        _localizer = localizer;
        _logger = logger;
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData[ViewDataReturnUrl] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData[ViewDataReturnUrl] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return await RedirectToLocal(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, _localizer["Login.AccountLockedOut"]);
            return View(model);
        }

        ModelState.AddModelError(string.Empty, _localizer["Login.InvalidCredentials"]);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    [AllowAnonymous]
    public async Task<IActionResult> Register(string? returnUrl = null)
    {
        ViewData[ViewDataReturnUrl] = returnUrl;
        await PopulateOrganisationDropdown();
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData[ViewDataReturnUrl] = returnUrl;

        if (!ModelState.IsValid)
        {
            await PopulateOrganisationDropdown(model.OrganisationId);
            return View(model);
        }

        var user = new CedevaUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            OrganisationId = model.OrganisationId
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Assign default role (Coordinator)
            await _userManager.AddToRoleAsync(user, "Coordinator");

            // Get organisation name for welcome email
            var organisation = await _organisationRepository.GetByIdAsync(model.OrganisationId);
            var organisationName = organisation?.Name ?? "Cedeva";

            // Send welcome email
            try
            {
                await _emailService.SendWelcomeEmailAsync(
                    user.Email,
                    $"{user.FirstName} {user.LastName}",
                    organisationName);
            }
            catch (Exception ex)
            {
                // Log error but don't block registration
                // Email sending failure should not prevent user from using the app
                _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
                TempData[ControllerExtensions.WarningMessageKey] = string.Format(_localizer["Message.WelcomeEmailFailed"].Value, ex.Message);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return await RedirectToLocal(returnUrl);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var model = new ProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            OrganisationId = user.OrganisationId
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ProfileUpdated"].Value;
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task<IActionResult> RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // Get current user
        var user = await _userManager.GetUserAsync(User);

        // For coordinators, check if there's a selected activity
        if (user?.Role == Core.Enums.Role.Coordinator)
        {
            // First check session
            var activityIdStr = HttpContext.Session.GetString("Activity_Id");

            // If not in session, try to restore from persistent cookie
            if (string.IsNullOrEmpty(activityIdStr))
            {
                activityIdStr = Request.Cookies["SelectedActivityId"];

                // Restore session from cookie
                if (!string.IsNullOrEmpty(activityIdStr) && int.TryParse(activityIdStr, out int cookieActivityId))
                {
                    HttpContext.Session.SetString("Activity_Id", cookieActivityId.ToString());
                    return RedirectToAction("Index", "ActivityManagement", new { id = cookieActivityId });
                }
            }
            else if (int.TryParse(activityIdStr, out int sessionActivityId))
            {
                return RedirectToAction("Index", "ActivityManagement", new { id = sessionActivityId });
            }
        }

        // Default: redirect to Home (for Admin or Coordinator without activity)
        return RedirectToAction("Index", "Home");
    }

    private async Task PopulateOrganisationDropdown(int? selectedOrganisationId = null)
    {
        var organisations = await _organisationRepository.GetAllAsync();
        var organisationList = organisations.OrderBy(o => o.Name)
            .Select(o => new SelectListItem
            {
                Value = o.Id.ToString(),
                Text = o.Name,
                Selected = o.Id == selectedOrganisationId
            })
            .ToList();

        ViewBag.Organisations = organisationList;
    }
}
