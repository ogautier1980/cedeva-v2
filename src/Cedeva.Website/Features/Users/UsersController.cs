using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Website.Features.Users.ViewModels;
using Cedeva.Infrastructure.Data;

namespace Cedeva.Website.Features.Users;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<CedevaUser> _userManager;
    private readonly CedevaDbContext _context;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UsersController(
        UserManager<CedevaUser> userManager,
        CedevaDbContext context,
        IStringLocalizer<SharedResources> localizer)
    {
        _userManager = userManager;
        _context = context;
        _localizer = localizer;
    }

    // GET: Users
    public async Task<IActionResult> Index(string? searchString, int? organisationId, int pageNumber = 1, int pageSize = 10)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var query = _userManager.Users
            .Include(u => u.Organisation)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(u =>
                u.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                u.Email!.Contains(searchString, StringComparison.OrdinalIgnoreCase));
        }

        if (organisationId.HasValue)
        {
            query = query.Where(u => u.OrganisationId == organisationId.Value);
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserViewModel
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email!,
                OrganisationId = u.OrganisationId,
                OrganisationName = u.Organisation != null ? u.Organisation.Name : "",
                Role = u.Role,
                EmailConfirmed = u.EmailConfirmed,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow
            })
            .ToListAsync();

        ViewData["SearchString"] = searchString;
        ViewData["OrganisationId"] = organisationId;
        ViewData["PageNumber"] = pageNumber;
        ViewData["PageSize"] = pageSize;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalItems"] = totalItems;

        await PopulateOrganisationDropdown(organisationId);

        return View(users);
    }

    // GET: Users/Details/5
    public async Task<IActionResult> Details(string id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var viewModel = await GetUserViewModelAsync(id);

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    // GET: Users/Create
    public async Task<IActionResult> Create()
    {
        await PopulateOrganisationDropdown();

        var viewModel = new UserViewModel
        {
            EmailConfirmed = true,
            Role = Role.Coordinator
        };

        return View(viewModel);
    }

    // POST: Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserViewModel viewModel)
    {
        if (string.IsNullOrEmpty(viewModel.Password))
        {
            ModelState.AddModelError(nameof(viewModel.Password), "Le mot de passe est requis.");
        }

        if (ModelState.IsValid)
        {
            var user = new CedevaUser
            {
                UserName = viewModel.Email,
                Email = viewModel.Email,
                FirstName = viewModel.FirstName,
                LastName = viewModel.LastName,
                OrganisationId = viewModel.OrganisationId,
                Role = viewModel.Role,
                EmailConfirmed = viewModel.EmailConfirmed
            };

            var result = await _userManager.CreateAsync(user, viewModel.Password!);

            if (result.Succeeded)
            {
                // Assign role
                var roleName = viewModel.Role == Role.Admin ? "Admin" : "Coordinator";
                await _userManager.AddToRoleAsync(user, roleName);

                TempData["SuccessMessage"] = _localizer["Message.UserCreated"];
                return RedirectToAction(nameof(Details), new { id = user.Id });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        await PopulateOrganisationDropdown(viewModel.OrganisationId);
        return View(viewModel);
    }

    // GET: Users/Edit/5
    public async Task<IActionResult> Edit(string id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        var viewModel = new UserViewModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email!,
            OrganisationId = user.OrganisationId,
            Role = user.Role,
            EmailConfirmed = user.EmailConfirmed
        };

        await PopulateOrganisationDropdown(viewModel.OrganisationId);

        return View(viewModel);
    }

    // POST: Users/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserViewModel viewModel)
    {
        if (id != viewModel.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            user.FirstName = viewModel.FirstName;
            user.LastName = viewModel.LastName;
            user.Email = viewModel.Email;
            user.UserName = viewModel.Email;
            user.OrganisationId = viewModel.OrganisationId;
            user.Role = viewModel.Role;
            user.EmailConfirmed = viewModel.EmailConfirmed;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                // Update password if provided
                if (!string.IsNullOrEmpty(viewModel.Password))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    await _userManager.ResetPasswordAsync(user, token, viewModel.Password);
                }

                // Update role
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                var roleName = viewModel.Role == Role.Admin ? "Admin" : "Coordinator";
                await _userManager.AddToRoleAsync(user, roleName);

                TempData["SuccessMessage"] = _localizer["Message.UserUpdated"];
                return RedirectToAction(nameof(Details), new { id = user.Id });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        await PopulateOrganisationDropdown(viewModel.OrganisationId);
        return View(viewModel);
    }

    // GET: Users/Delete/5
    public async Task<IActionResult> Delete(string id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var viewModel = await GetUserViewModelAsync(id);

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    // POST: Users/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        var result = await _userManager.DeleteAsync(user);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = _localizer["Message.UserDeleted"];
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return RedirectToAction(nameof(Delete), new { id });
    }

    private async Task PopulateOrganisationDropdown(int? selectedOrganisationId = null)
    {
        var organisations = await _context.Organisations
            .OrderBy(o => o.Name)
            .ToListAsync();

        ViewBag.Organisations = organisations.Select(o => new SelectListItem
        {
            Value = o.Id.ToString(),
            Text = o.Name,
            Selected = o.Id == selectedOrganisationId
        }).ToList();
    }

    // Helper method to get user view model
    private async Task<UserViewModel?> GetUserViewModelAsync(string id)
    {
        var user = await _userManager.Users
            .Include(u => u.Organisation)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return null;
        }

        return new UserViewModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email!,
            OrganisationId = user.OrganisationId,
            OrganisationName = user.Organisation != null ? user.Organisation.Name : "",
            Role = user.Role,
            EmailConfirmed = user.EmailConfirmed,
            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow
        };
    }
}
