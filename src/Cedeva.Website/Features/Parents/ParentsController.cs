using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Helpers;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Parents.ViewModels;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Cedeva.Website.Infrastructure;

namespace Cedeva.Website.Features.Parents;

[Authorize]
public class ParentsController : Controller
{
    private const string SessionKeyParentsSearchString = SessionKeyParentsSearchString;
    private const string SessionKeyParentsSortBy = SessionKeyParentsSortBy;
    private const string SessionKeyParentsSortOrder = SessionKeyParentsSortOrder;
    private const string SessionKeyParentsPageNumber = SessionKeyParentsPageNumber;
    private const string SortOrderDescending = "desc";

    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ParentsController> _logger;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IUserDisplayService _userDisplayService;
    private readonly ISessionStateService _sessionState;

    public ParentsController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ParentsController> logger,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IStringLocalizer<SharedResources> localizer,
        IUserDisplayService userDisplayService,
        ISessionStateService sessionState)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _localizer = localizer;
        _userDisplayService = userDisplayService;
        _sessionState = sessionState;
    }

    public async Task<IActionResult> Index([FromQuery] ParentQueryParameters queryParams)
    {
        // Check if any query parameters were provided in the actual HTTP request
        bool hasQueryParams = Request.Query.Count > 0;

        // If query params provided, store them and redirect to clean URL
        if (hasQueryParams)
        {
            if (!string.IsNullOrWhiteSpace(queryParams.SearchString))
                _sessionState.Set(SessionKeyParentsSearchString, queryParams.SearchString, persistToCookie: false);

            if (!string.IsNullOrWhiteSpace(queryParams.SortBy))
                _sessionState.Set(SessionKeyParentsSortBy, queryParams.SortBy, persistToCookie: false);

            if (!string.IsNullOrWhiteSpace(queryParams.SortOrder))
                _sessionState.Set(SessionKeyParentsSortOrder, queryParams.SortOrder, persistToCookie: false);

            if (queryParams.PageNumber > 1)
                _sessionState.Set(SessionKeyParentsPageNumber, queryParams.PageNumber.ToString(), persistToCookie: false);

            // Mark that filters should be kept for the next request (after redirect)
            TempData[ControllerExtensions.KeepFiltersKey] = true;

            // Redirect to clean URL
            return RedirectToAction(nameof(Index));
        }

        // If not keeping filters (no redirect, just navigation/F5), clear them
        if (TempData[ControllerExtensions.KeepFiltersKey] == null)
        {
            _sessionState.Clear(SessionKeyParentsSearchString);
            _sessionState.Clear(SessionKeyParentsSortBy);
            _sessionState.Clear(SessionKeyParentsSortOrder);
            _sessionState.Clear(SessionKeyParentsPageNumber);
        }

        // Load filters from state (will be empty if just cleared)
        queryParams.SearchString = _sessionState.Get(SessionKeyParentsSearchString);
        queryParams.SortBy = _sessionState.Get(SessionKeyParentsSortBy);
        queryParams.SortOrder = _sessionState.Get(SessionKeyParentsSortOrder);

        var pageNumberStr = _sessionState.Get(SessionKeyParentsPageNumber);
        if (!string.IsNullOrEmpty(pageNumberStr) && int.TryParse(pageNumberStr, out var pageNum))
        {
            queryParams.PageNumber = pageNum;
        }

        var query = _context.Parents
            .Include(p => p.Address)
            .Include(p => p.Children)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryParams.SearchString))
        {
            query = query.Where(p =>
                p.FirstName.Contains(queryParams.SearchString) ||
                p.LastName.Contains(queryParams.SearchString) ||
                p.Email.Contains(queryParams.SearchString));
        }

        // Apply sorting
        query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortOrder?.ToLowerInvariant()) switch
        {
            ("firstname", "asc") => query.OrderBy(p => p.FirstName),
            ("firstname", SortOrderDescending) => query.OrderByDescending(p => p.FirstName),
            ("lastname", SortOrderDescending) => query.OrderByDescending(p => p.LastName),
            ("email", "asc") => query.OrderBy(p => p.Email),
            ("email", SortOrderDescending) => query.OrderByDescending(p => p.Email),
            ("city", "asc") => query.OrderBy(p => p.Address.City),
            ("city", SortOrderDescending) => query.OrderByDescending(p => p.Address.City),
            _ => query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
        };

        var pagedResult = await query
            .Select(p => new ParentViewModel
            {
                Id = p.Id,
                FirstName = p.FirstName,
                LastName = p.LastName,
                Email = p.Email,
                PhoneNumber = p.PhoneNumber,
                MobilePhoneNumber = p.MobilePhoneNumber,
                NationalRegisterNumber = p.NationalRegisterNumber,
                Street = p.Address.Street,
                City = p.Address.City,
                PostalCode = p.Address.PostalCode,
                Country = p.Address.Country,
                ChildrenCount = p.Children.Count
            })
            .ToPaginatedListAsync(queryParams.PageNumber, queryParams.PageSize);

        ViewData["SortBy"] = queryParams.SortBy;
        ViewData["SortOrder"] = queryParams.SortOrder;
        ViewData["PageNumber"] = pagedResult.PageNumber;
        ViewData["PageSize"] = pagedResult.PageSize;
        ViewData["TotalPages"] = pagedResult.TotalPages;
        ViewData["TotalItems"] = pagedResult.TotalItems;

        var viewModel = new ParentListViewModel
        {
            Parents = pagedResult.Items,
            SearchTerm = queryParams.SearchString,
            CurrentPage = pagedResult.PageNumber,
            TotalPages = pagedResult.TotalPages,
            PageSize = pagedResult.PageSize
        };

        return View(viewModel);
    }

    // GET: Parents/Export
    public async Task<IActionResult> Export(string? searchTerm)
    {
        var query = _context.Parents
            .Include(p => p.Address)
            .Include(p => p.Children)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p =>
                p.FirstName.Contains(searchTerm) ||
                p.LastName.Contains(searchTerm) ||
                p.Email.Contains(searchTerm));
        }

        var parents = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();

        var columns = new Dictionary<string, Func<Parent, object>>
        {
            { _localizer["Excel.FirstName"], p => p.FirstName },
            { _localizer["Excel.LastName"], p => p.LastName },
            { _localizer["Excel.Email"], p => p.Email },
            { _localizer["Excel.Phone"], p => p.PhoneNumber ?? "" },
            { _localizer["Excel.MobilePhone"], p => p.MobilePhoneNumber ?? "" },
            { _localizer["Excel.NationalRegisterNumber"], p => p.NationalRegisterNumber },
            { _localizer["Excel.Street"], p => p.Address?.Street ?? "" },
            { _localizer["Excel.PostalCode"], p => p.Address?.PostalCode ?? "" },
            { _localizer["Excel.City"], p => p.Address?.City ?? "" },
            { _localizer["Excel.Country"], p => p.Address?.Country.ToString() ?? "" },
            { _localizer["Excel.ChildrenCount"], p => p.Children.Count }
        };

        var sheetName = _localizer["Excel.ParentsSheet"];
        var excelData = _excelExportService.ExportToExcel(parents, sheetName, columns);
        var fileName = $"{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Parents/ExportPdf
    public async Task<IActionResult> ExportPdf(string? searchTerm)
    {
        var query = _context.Parents
            .Include(p => p.Address)
            .Include(p => p.Children)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p =>
                p.FirstName.Contains(searchTerm) ||
                p.LastName.Contains(searchTerm) ||
                p.Email.Contains(searchTerm));
        }

        var parents = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();

        var columns = new Dictionary<string, Func<Parent, object>>
        {
            { _localizer["Excel.FirstName"], p => p.FirstName },
            { _localizer["Excel.LastName"], p => p.LastName },
            { _localizer["Excel.Email"], p => p.Email },
            { _localizer["Excel.Phone"], p => p.PhoneNumber ?? "" },
            { _localizer["Excel.MobilePhone"], p => p.MobilePhoneNumber ?? "" },
            { _localizer["Excel.Street"], p => p.Address?.Street ?? "" },
            { _localizer["Excel.PostalCode"], p => p.Address?.PostalCode ?? "" },
            { _localizer["Excel.City"], p => p.Address?.City ?? "" },
            { _localizer["Excel.ChildrenCount"], p => p.Children.Count }
        };

        var title = _localizer["Excel.ParentsSheet"];
        var pdfData = _pdfExportService.ExportToPdf(parents, title, columns);
        var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(pdfData, "application/pdf", fileName);
    }

    public async Task<IActionResult> Details(int id)
    {
        var viewModel = await GetParentViewModelAsync(id);

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    public async Task<IActionResult> Create()
    {
        var viewModel = new ParentViewModel
        {
            Country = Country.Belgium,
            OrganisationId = _currentUserService.OrganisationId ?? 0
        };

        // For admins, populate organisation dropdown
        if (_currentUserService.IsAdmin)
        {
            var organisations = await _context.Organisations
                .OrderBy(o => o.Name)
                .Select(o => new { o.Id, o.Name })
                .ToListAsync();
            ViewBag.Organisations = new SelectList(organisations, "Id", "Name", viewModel.OrganisationId);
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ParentViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOrganisationsDropdown(viewModel.OrganisationId);
            return View(viewModel);
        }

        var organisationId = _currentUserService.OrganisationId;
        if (!_currentUserService.IsAdmin && organisationId == null)
        {
            return Forbid();
        }

        var address = new Address
        {
            Street = viewModel.Street,
            City = viewModel.City,
            PostalCode = viewModel.PostalCode,
            Country = viewModel.Country
        };

        var parent = new Parent
        {
            FirstName = viewModel.FirstName,
            LastName = viewModel.LastName,
            Email = viewModel.Email,
            PhoneNumber = viewModel.PhoneNumber,
            MobilePhoneNumber = viewModel.MobilePhoneNumber,
            NationalRegisterNumber = NationalRegisterNumberHelper.StripFormatting(viewModel.NationalRegisterNumber),
            Address = address,
            OrganisationId = _currentUserService.IsAdmin ? viewModel.OrganisationId : organisationId!.Value
        };

        _context.Parents.Add(parent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Parent {Name} created by user {UserId}", parent.FullName, _currentUserService.UserId);

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ParentCreated"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAjax([FromForm] ParentViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );
            return Json(new { success = false, errors });
        }

        var organisationId = _currentUserService.OrganisationId;
        if (!_currentUserService.IsAdmin && organisationId == null)
        {
            return Json(new { success = false, message = _localizer["Error.Unauthorized"].Value });
        }

        var address = new Address
        {
            Street = viewModel.Street,
            City = viewModel.City,
            PostalCode = viewModel.PostalCode,
            Country = viewModel.Country
        };

        var parent = new Parent
        {
            FirstName = viewModel.FirstName,
            LastName = viewModel.LastName,
            Email = viewModel.Email,
            PhoneNumber = viewModel.PhoneNumber,
            MobilePhoneNumber = viewModel.MobilePhoneNumber,
            NationalRegisterNumber = NationalRegisterNumberHelper.StripFormatting(viewModel.NationalRegisterNumber),
            Address = address,
            OrganisationId = _currentUserService.IsAdmin ? viewModel.OrganisationId : organisationId!.Value
        };

        _context.Parents.Add(parent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Parent {Name} created via AJAX by user {UserId}", parent.FullName, _currentUserService.UserId);

        return Json(new
        {
            success = true,
            parentId = parent.Id,
            parentName = parent.FullName,
            message = _localizer["Message.ParentCreated"].Value
        });
    }

    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var parent = await _context.Parents
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (parent == null)
        {
            return NotFound();
        }

        var viewModel = MapToViewModel(parent);

        if (_currentUserService.IsAdmin)
        {
            var organisations = await _context.Organisations
                .OrderBy(o => o.Name)
                .Select(o => new { o.Id, o.Name })
                .ToListAsync();
            ViewBag.Organisations = new SelectList(organisations, "Id", "Name", viewModel.OrganisationId);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ParentViewModel viewModel, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (id != viewModel.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateOrganisationsDropdown(viewModel.OrganisationId);
            return View(viewModel);
        }

        var parent = await _context.Parents
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (parent == null)
        {
            return NotFound();
        }

        parent.FirstName = viewModel.FirstName;
        parent.LastName = viewModel.LastName;
        parent.Email = viewModel.Email;
        parent.PhoneNumber = viewModel.PhoneNumber;
        parent.MobilePhoneNumber = viewModel.MobilePhoneNumber;
        parent.NationalRegisterNumber = NationalRegisterNumberHelper.StripFormatting(viewModel.NationalRegisterNumber);

        // Update OrganisationId if admin
        if (_currentUserService.IsAdmin)
        {
            parent.OrganisationId = viewModel.OrganisationId;
        }

        parent.Address.Street = viewModel.Street;
        parent.Address.City = viewModel.City;
        parent.Address.PostalCode = viewModel.PostalCode;
        parent.Address.Country = viewModel.Country;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Parent {Name} updated by user {UserId}", parent.FullName, _currentUserService.UserId);
            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ParentUpdated"].Value;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (!await ParentExists(id))
            {
                return NotFound();
            }
            _logger.LogError(ex, "Concurrency error updating parent {Id}", id);
            throw new InvalidOperationException($"Failed to update parent {id} due to concurrency conflict", ex);
        }

        return this.RedirectToReturnUrlOrAction(returnUrl, nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var viewModel = await GetParentViewModelAsync(id);
        return viewModel == null ? NotFound() : View(viewModel);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        var parent = await _context.Parents
            .Include(p => p.Children)
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (parent == null)
        {
            return NotFound();
        }

        if (parent.Children.Any())
        {
            TempData[ControllerExtensions.ErrorMessageKey] = _localizer["Message.ParentHasChildren"].Value;
            return RedirectToAction(nameof(Index));
        }

        _context.Addresses.Remove(parent.Address);
        _context.Parents.Remove(parent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Parent {Name} deleted by user {UserId}", parent.FullName, _currentUserService.UserId);
        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.ParentDeleted"].Value;

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOrganisationsDropdown(int? selectedOrganisationId = null)
    {
        if (_currentUserService.IsAdmin)
        {
            var organisations = await _context.Organisations
                .OrderBy(o => o.Name)
                .Select(o => new { o.Id, o.Name })
                .ToListAsync();
            ViewBag.Organisations = new SelectList(organisations, "Id", "Name", selectedOrganisationId);
        }
    }

    private async Task<bool> ParentExists(int id)
    {
        return await _context.Parents.AnyAsync(p => p.Id == id);
    }

    // Helper method to get parent view model with all related data
    private async Task<ParentViewModel?> GetParentViewModelAsync(int id)
    {
        var parent = await _context.Parents
            .Include(p => p.Address)
            .Include(p => p.Children)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (parent == null)
        {
            return null;
        }

        var viewModel = MapToViewModel(parent);

        // Fetch user display names for audit fields
        viewModel.CreatedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(parent.CreatedBy);
        if (!string.IsNullOrEmpty(parent.ModifiedBy))
        {
            viewModel.ModifiedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(parent.ModifiedBy);
        }

        return viewModel;
    }

    private static ParentViewModel MapToViewModel(Parent parent)
    {
        return new ParentViewModel
        {
            Id = parent.Id,
            FirstName = parent.FirstName,
            LastName = parent.LastName,
            Email = parent.Email,
            PhoneNumber = parent.PhoneNumber,
            MobilePhoneNumber = parent.MobilePhoneNumber,
            NationalRegisterNumber = NationalRegisterNumberHelper.Format(parent.NationalRegisterNumber),
            Street = parent.Address?.Street ?? string.Empty,
            City = parent.Address?.City ?? string.Empty,
            PostalCode = parent.Address?.PostalCode ?? "",
            Country = parent.Address?.Country ?? Country.Belgium,
            AddressId = parent.AddressId,
            OrganisationId = parent.OrganisationId,
            ChildrenCount = parent.Children?.Count ?? 0,
            Children = parent.Children?.Select(c => new ChildSummaryViewModel
            {
                Id = c.Id,
                FullName = c.FullName,
                BirthDate = c.BirthDate
            }).ToList() ?? new List<ChildSummaryViewModel>(),

            // Audit fields
            CreatedAt = parent.CreatedAt,
            CreatedBy = parent.CreatedBy,
            ModifiedAt = parent.ModifiedAt,
            ModifiedBy = parent.ModifiedBy
        };
    }

}
