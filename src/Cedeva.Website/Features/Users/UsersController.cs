using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Users.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Cedeva.Infrastructure.Data;

namespace Cedeva.Website.Features.Users;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private const string SortOrderDescending = "desc";
    private const string SortOrderAscending = "asc";

    private readonly UserManager<CedevaUser> _userManager;
    private readonly CedevaDbContext _context;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IUserDisplayService _userDisplayService;

    public UsersController(
        UserManager<CedevaUser> userManager,
        CedevaDbContext context,
        IStringLocalizer<SharedResources> localizer,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IUserDisplayService userDisplayService)
    {
        _userManager = userManager;
        _context = context;
        _localizer = localizer;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _userDisplayService = userDisplayService;
    }

    // GET: Users
    public async Task<IActionResult> Index([FromQuery] UserQueryParameters queryParams)
    {
        var query = _userManager.Users
            .Include(u => u.Organisation)
            .AsQueryable();

        if (!string.IsNullOrEmpty(queryParams.SearchString))
        {
            query = query.Where(u =>
                u.FirstName.Contains(queryParams.SearchString, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(queryParams.SearchString, StringComparison.OrdinalIgnoreCase) ||
                u.Email!.Contains(queryParams.SearchString, StringComparison.OrdinalIgnoreCase));
        }

        if (queryParams.OrganisationId.HasValue)
        {
            query = query.Where(u => u.OrganisationId == queryParams.OrganisationId.Value);
        }

        // Apply sorting
        query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortOrder?.ToLowerInvariant()) switch
        {
            ("firstname", SortOrderAscending) => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName),
            ("firstname", SortOrderDescending) => query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName),
            ("lastname", SortOrderDescending) => query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName),
            ("email", SortOrderAscending) => query.OrderBy(u => u.Email),
            ("email", SortOrderDescending) => query.OrderByDescending(u => u.Email),
            ("role", SortOrderAscending) => query.OrderBy(u => u.Role),
            ("role", SortOrderDescending) => query.OrderByDescending(u => u.Role),
            ("organisationname", SortOrderAscending) => query.OrderBy(u => u.Organisation!.Name),
            ("organisationname", SortOrderDescending) => query.OrderByDescending(u => u.Organisation!.Name),
            _ => query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName) // default
        };

        var pagedResult = await query
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
            .ToPaginatedListAsync(queryParams.PageNumber, queryParams.PageSize);

        ViewData["SearchString"] = queryParams.SearchString;
        ViewData["OrganisationId"] = queryParams.OrganisationId;
        ViewData["SortBy"] = queryParams.SortBy;
        ViewData["SortOrder"] = queryParams.SortOrder;
        ViewData["PageNumber"] = pagedResult.PageNumber;
        ViewData["PageSize"] = pagedResult.PageSize;
        ViewData["TotalPages"] = pagedResult.TotalPages;
        ViewData["TotalItems"] = pagedResult.TotalItems;

        await PopulateOrganisationDropdown(queryParams.OrganisationId);

        return View(pagedResult.Items);
    }

    // GET: Users/Details/5
    public async Task<IActionResult> Details(string id)
    {
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

                TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.UserCreated"].Value;
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
                await UpdateUserPasswordIfProvided(user, viewModel.Password);
                await UpdateUserRole(user, viewModel.Role);

                TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.UserUpdated"].Value;
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
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var viewModel = await GetUserViewModelAsync(id);
        return viewModel == null ? NotFound() : View(viewModel);
    }

    // POST: Users/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        var result = await _userManager.DeleteAsync(user);

        if (result.Succeeded)
        {
            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.UserDeleted"].Value;
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return RedirectToAction(nameof(Delete), new { id });
    }

    private async Task UpdateUserPasswordIfProvided(CedevaUser user, string? password)
    {
        if (!string.IsNullOrEmpty(password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, password);
        }
    }

    private async Task UpdateUserRole(CedevaUser user, Role role)
    {
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        var roleName = role == Role.Admin ? "Admin" : "Coordinator";
        await _userManager.AddToRoleAsync(user, roleName);
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

        var viewModel = new UserViewModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email!,
            OrganisationId = user.OrganisationId,
            OrganisationName = user.Organisation != null ? user.Organisation.Name : "",
            Role = user.Role,
            EmailConfirmed = user.EmailConfirmed,
            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,

            // Audit fields
            CreatedAt = user.CreatedAt,
            CreatedBy = user.CreatedBy,
            ModifiedAt = user.ModifiedAt,
            ModifiedBy = user.ModifiedBy
        };

        // Fetch user display names for audit fields
        viewModel.CreatedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(user.CreatedBy);
        if (!string.IsNullOrEmpty(user.ModifiedBy))
        {
            viewModel.ModifiedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(user.ModifiedBy);
        }

        return viewModel;
    }


    // GET: Users/Export
    public async Task<IActionResult> Export(string? searchString, int? organisationId)
    {
        var query = _userManager.Users
            .Include(u => u.Organisation)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(u =>
                u.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                (u.Email != null && u.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase)));
        }

        if (organisationId.HasValue)
        {
            query = query.Where(u => u.OrganisationId == organisationId.Value);
        }

        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

        var columns = new Dictionary<string, Func<CedevaUser, object>>
        {
            { _localizer["Excel.FirstName"], u => u.FirstName },
            { _localizer["Excel.LastName"], u => u.LastName },
            { _localizer["Excel.Email"], u => u.Email ?? "" },
            { _localizer["Excel.Organisation"], u => u.Organisation?.Name ?? "" },
            { _localizer["Excel.Role"], u => _localizer[$"Enum.Role.{u.Role}"].Value },
            { _localizer["Excel.EmailConfirmed"], u => u.EmailConfirmed },
            { _localizer["Excel.Status"], u => u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow ? _localizer["LockedOut"].Value : _localizer["Active"].Value }
        };

        var sheetName = _localizer["Excel.UsersSheet"];
        var excelData = _excelExportService.ExportToExcel(users, sheetName, columns);
        var fileName = $"{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Users/ExportPdf
    public async Task<IActionResult> ExportPdf(string? searchString, int? organisationId)
    {
        var query = _userManager.Users
            .Include(u => u.Organisation)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(u =>
                u.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                (u.Email != null && u.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase)));
        }

        if (organisationId.HasValue)
        {
            query = query.Where(u => u.OrganisationId == organisationId.Value);
        }

        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

        var columns = new Dictionary<string, Func<CedevaUser, object>>
        {
            { _localizer["Excel.FirstName"], u => u.FirstName },
            { _localizer["Excel.LastName"], u => u.LastName },
            { _localizer["Excel.Email"], u => u.Email ?? "" },
            { _localizer["Excel.Organisation"], u => u.Organisation?.Name ?? "" },
            { _localizer["Excel.Role"], u => _localizer[$"Enum.Role.{u.Role}"].Value },
            { _localizer["Excel.EmailConfirmed"], u => u.EmailConfirmed },
            { _localizer["Excel.Status"], u => u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow ? _localizer["LockedOut"].Value : _localizer["Active"].Value }
        };

        var title = _localizer["Excel.UsersSheet"];
        var pdfData = _pdfExportService.ExportToPdf(users, title, columns);
        var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(pdfData, "application/pdf", fileName);
    }
}
