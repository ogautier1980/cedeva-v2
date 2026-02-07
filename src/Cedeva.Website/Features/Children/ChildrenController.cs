using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Children.ViewModels;
using Cedeva.Website.Localization;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Cedeva.Website.Infrastructure;

namespace Cedeva.Website.Features.Children;

[Authorize]
public class ChildrenController : Controller
{
    private const string TempDataSuccessMessage = "SuccessMessage";
    private const string SortOrderDescending = "desc";

    private readonly IRepository<Child> _childRepository;
    private readonly IRepository<Parent> _parentRepository;
    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IUserDisplayService _userDisplayService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISessionStateService _sessionState;

    public ChildrenController(
        IRepository<Child> childRepository,
        IRepository<Parent> parentRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IStringLocalizer<SharedResources> localizer,
        IUserDisplayService userDisplayService,
        ICurrentUserService currentUserService,
        ISessionStateService sessionState)
    {
        _childRepository = childRepository;
        _parentRepository = parentRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _localizer = localizer;
        _userDisplayService = userDisplayService;
        _currentUserService = currentUserService;
        _sessionState = sessionState;
    }

    // GET: Children
    public async Task<IActionResult> Index([FromQuery] ChildQueryParameters queryParams)
    {
        // Check if any query parameters were provided in the actual HTTP request
        bool hasQueryParams = Request.Query.Count > 0;

        // If query params provided, store them and redirect to clean URL
        if (hasQueryParams)
        {
            if (!string.IsNullOrWhiteSpace(queryParams.SearchString))
                _sessionState.Set("Children_SearchString", queryParams.SearchString, persistToCookie: false);

            if (queryParams.ParentId.HasValue)
                _sessionState.Set("Children_ParentId", queryParams.ParentId, persistToCookie: false);

            if (!string.IsNullOrWhiteSpace(queryParams.SortBy))
                _sessionState.Set("Children_SortBy", queryParams.SortBy, persistToCookie: false);

            if (!string.IsNullOrWhiteSpace(queryParams.SortOrder))
                _sessionState.Set("Children_SortOrder", queryParams.SortOrder, persistToCookie: false);

            if (queryParams.PageNumber > 1)
                _sessionState.Set("Children_PageNumber", queryParams.PageNumber.ToString(), persistToCookie: false);

            // Mark that filters should be kept for the next request (after redirect)
            TempData["KeepFilters"] = true;

            // Redirect to clean URL
            return RedirectToAction(nameof(Index));
        }

        // If not keeping filters (no redirect, just navigation/F5), clear them
        if (TempData["KeepFilters"] == null)
        {
            _sessionState.Clear("Children_SearchString");
            _sessionState.Clear("Children_ParentId");
            _sessionState.Clear("Children_SortBy");
            _sessionState.Clear("Children_SortOrder");
            _sessionState.Clear("Children_PageNumber");
        }

        // Load filters from state (will be empty if just cleared)
        queryParams.SearchString = _sessionState.Get("Children_SearchString");
        queryParams.ParentId = _sessionState.Get<int>("Children_ParentId");
        queryParams.SortBy = _sessionState.Get("Children_SortBy");
        queryParams.SortOrder = _sessionState.Get("Children_SortOrder");

        var pageNumberStr = _sessionState.Get("Children_PageNumber");
        if (!string.IsNullOrEmpty(pageNumberStr) && int.TryParse(pageNumberStr, out var pageNum))
        {
            queryParams.PageNumber = pageNum;
        }

        var query = _context.Children.AsQueryable();

        if (!string.IsNullOrEmpty(queryParams.SearchString))
        {
            query = query.Where(c =>
                c.FirstName.Contains(queryParams.SearchString) ||
                c.LastName.Contains(queryParams.SearchString) ||
                c.NationalRegisterNumber.Contains(queryParams.SearchString));
        }

        if (queryParams.ParentId.HasValue)
        {
            query = query.Where(c => c.ParentId == queryParams.ParentId.Value);
        }

        // Apply sorting
        query = (queryParams.SortBy?.ToLower(), queryParams.SortOrder?.ToLower()) switch
        {
            ("firstname", "asc") => query.OrderBy(c => c.FirstName).ThenBy(c => c.LastName),
            ("firstname", SortOrderDescending) => query.OrderByDescending(c => c.FirstName).ThenByDescending(c => c.LastName),
            ("lastname", SortOrderDescending) => query.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName),
            ("birthdate", "asc") => query.OrderBy(c => c.BirthDate),
            ("birthdate", SortOrderDescending) => query.OrderByDescending(c => c.BirthDate),
            ("nationalregisternumber", "asc") => query.OrderBy(c => c.NationalRegisterNumber),
            ("nationalregisternumber", SortOrderDescending) => query.OrderByDescending(c => c.NationalRegisterNumber),
            _ => query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName) // default
        };

        var pagedResult = await query
            .Select(c => new ChildViewModel
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                NationalRegisterNumber = c.NationalRegisterNumber,
                BirthDate = c.BirthDate,
                IsDisadvantagedEnvironment = c.IsDisadvantagedEnvironment,
                IsMildDisability = c.IsMildDisability,
                IsSevereDisability = c.IsSevereDisability,
                ParentId = c.ParentId,
                ParentFullName = c.Parent != null ? $"{c.Parent.FirstName} {c.Parent.LastName}" : "",
                ActivityGroupId = c.ActivityGroupId
            })
            .ToPaginatedListAsync(queryParams.PageNumber, queryParams.PageSize);

        ViewData["SearchString"] = queryParams.SearchString;
        ViewData["ParentId"] = queryParams.ParentId;
        ViewData["SortBy"] = queryParams.SortBy;
        ViewData["SortOrder"] = queryParams.SortOrder;
        ViewData["PageNumber"] = pagedResult.PageNumber;
        ViewData["PageSize"] = pagedResult.PageSize;
        ViewData["TotalPages"] = pagedResult.TotalPages;
        ViewData["TotalItems"] = pagedResult.TotalItems;

        return View(pagedResult.Items);
    }

    // GET: Children/Export
    public async Task<IActionResult> Export(string? searchString, int? parentId)
    {
        var query = _context.Children
            .Include(c => c.Parent)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(c =>
                c.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                c.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                c.NationalRegisterNumber.Contains(searchString));
        }

        if (parentId.HasValue)
        {
            query = query.Where(c => c.ParentId == parentId.Value);
        }

        var children = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();

        var columns = new Dictionary<string, Func<Child, object>>
        {
            { _localizer["Excel.FirstName"], c => c.FirstName },
            { _localizer["Excel.LastName"], c => c.LastName },
            { _localizer["Excel.NationalRegisterNumber"], c => c.NationalRegisterNumber },
            { _localizer["Excel.BirthDate"], c => c.BirthDate },
            { _localizer["Excel.Age"], c => DateTime.Today.Year - c.BirthDate.Year - (DateTime.Today.DayOfYear < c.BirthDate.DayOfYear ? 1 : 0) },
            { _localizer["Excel.Parent"], c => c.Parent != null ? $"{c.Parent.FirstName} {c.Parent.LastName}" : "" },
            { _localizer["Excel.ParentEmail"], c => c.Parent?.Email ?? "" },
            { _localizer["Excel.ParentPhone"], c => c.Parent?.MobilePhoneNumber ?? c.Parent?.PhoneNumber ?? "" },
            { _localizer["Excel.DisadvantagedEnvironment"], c => c.IsDisadvantagedEnvironment },
            { _localizer["Excel.MildDisability"], c => c.IsMildDisability },
            { _localizer["Excel.SevereDisability"], c => c.IsSevereDisability }
        };

        var sheetName = _localizer["Excel.ChildrenSheet"];
        var excelData = _excelExportService.ExportToExcel(children, sheetName, columns);
        var fileName = $"{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Children/ExportPdf
    public async Task<IActionResult> ExportPdf(string? searchString, int? parentId)
    {
        var query = _context.Children
            .Include(c => c.Parent)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(c =>
                c.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                c.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                c.NationalRegisterNumber.Contains(searchString));
        }

        if (parentId.HasValue)
        {
            query = query.Where(c => c.ParentId == parentId.Value);
        }

        var children = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();

        var columns = new Dictionary<string, Func<Child, object>>
        {
            { _localizer["Excel.FirstName"], c => c.FirstName },
            { _localizer["Excel.LastName"], c => c.LastName },
            { _localizer["Excel.BirthDate"], c => c.BirthDate },
            { _localizer["Excel.Age"], c => DateTime.Today.Year - c.BirthDate.Year - (DateTime.Today.DayOfYear < c.BirthDate.DayOfYear ? 1 : 0) },
            { _localizer["Excel.Parent"], c => c.Parent != null ? $"{c.Parent.FirstName} {c.Parent.LastName}" : "" },
            { _localizer["Excel.ParentPhone"], c => c.Parent?.MobilePhoneNumber ?? c.Parent?.PhoneNumber ?? "" }
        };

        var title = _localizer["Excel.ChildrenSheet"];
        var pdfData = _pdfExportService.ExportToPdf(children, title, columns);
        var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(pdfData, "application/pdf", fileName);
    }

    // GET: Children/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var viewModel = await GetChildViewModelAsync(id);

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    // GET: Children/Create
    public async Task<IActionResult> Create(int? parentId)
    {
        await PopulateParentDropdown(parentId);

        var viewModel = new ChildViewModel
        {
            ParentId = parentId ?? 0,
            BirthDate = DateTime.Today.AddYears(-10) // Default date
        };

        // Pass organisation ID for inline parent creation
        ViewBag.CurrentOrganisationId = _currentUserService.OrganisationId ?? 0;

        return View(viewModel);
    }

    // POST: Children/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChildViewModel viewModel)
    {
        if (ModelState.IsValid)
        {
            var child = new Child
            {
                FirstName = viewModel.FirstName,
                LastName = viewModel.LastName,
                NationalRegisterNumber = viewModel.NationalRegisterNumber,
                BirthDate = viewModel.BirthDate,
                IsDisadvantagedEnvironment = viewModel.IsDisadvantagedEnvironment,
                IsMildDisability = viewModel.IsMildDisability,
                IsSevereDisability = viewModel.IsSevereDisability,
                ParentId = viewModel.ParentId,
                ActivityGroupId = viewModel.ActivityGroupId
            };

            await _childRepository.AddAsync(child);
            await _unitOfWork.SaveChangesAsync();

            TempData[TempDataSuccessMessage] = _localizer["Message.ChildCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = child.Id });
        }

        await PopulateParentDropdown(viewModel.ParentId);
        return View(viewModel);
    }

    // GET: Children/Edit/5
    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var child = await _childRepository.GetByIdAsync(id);

        if (child == null)
        {
            return NotFound();
        }

        var viewModel = new ChildViewModel
        {
            Id = child.Id,
            FirstName = child.FirstName,
            LastName = child.LastName,
            NationalRegisterNumber = child.NationalRegisterNumber,
            BirthDate = child.BirthDate,
            IsDisadvantagedEnvironment = child.IsDisadvantagedEnvironment,
            IsMildDisability = child.IsMildDisability,
            IsSevereDisability = child.IsSevereDisability,
            ParentId = child.ParentId,
            ActivityGroupId = child.ActivityGroupId
        };

        ViewData["ReturnUrl"] = returnUrl;
        await PopulateParentDropdown(child.ParentId);
        return View(viewModel);
    }

    // POST: Children/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ChildViewModel viewModel, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (id != viewModel.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var child = await _childRepository.GetByIdAsync(id);

            if (child == null)
            {
                return NotFound();
            }

            child.FirstName = viewModel.FirstName;
            child.LastName = viewModel.LastName;
            child.NationalRegisterNumber = viewModel.NationalRegisterNumber;
            child.BirthDate = viewModel.BirthDate;
            child.IsDisadvantagedEnvironment = viewModel.IsDisadvantagedEnvironment;
            child.IsMildDisability = viewModel.IsMildDisability;
            child.IsSevereDisability = viewModel.IsSevereDisability;
            child.ParentId = viewModel.ParentId;
            child.ActivityGroupId = viewModel.ActivityGroupId;

            await _childRepository.UpdateAsync(child);
            await _unitOfWork.SaveChangesAsync();

            TempData[TempDataSuccessMessage] = _localizer["Message.ChildUpdated"].Value;
            return this.RedirectToReturnUrlOrAction(returnUrl, nameof(Details), new { id = child.Id });
        }

        await PopulateParentDropdown(viewModel.ParentId);
        return View(viewModel);
    }

    // GET: Children/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var viewModel = await GetChildViewModelAsync(id);
        return viewModel == null ? NotFound() : View(viewModel);
    }

    // POST: Children/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        var child = await _childRepository.GetByIdAsync(id);

        if (child == null)
        {
            return NotFound();
        }

        await _childRepository.DeleteAsync(child);
        await _unitOfWork.SaveChangesAsync();

        TempData[TempDataSuccessMessage] = _localizer["Message.ChildDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    // Helper method to get child view model with all related data
    private async Task<ChildViewModel?> GetChildViewModelAsync(int id)
    {
        var child = await _childRepository.GetByIdAsync(id);

        if (child == null)
        {
            return null;
        }

        var parent = await _context.Parents.FindAsync(child.ParentId);
        var today = DateTime.Today;
        var bookings = await _context.Bookings
            .Include(b => b.Days)
            .Where(b => b.ChildId == id)
            .Select(b => new BookingSummaryViewModel
            {
                Id = b.Id,
                ActivityName = b.Activity.Name,
                StartDate = b.Activity.StartDate,
                EndDate = b.Activity.EndDate,
                IsConfirmed = b.IsConfirmed,
                DaysCount = b.Days.Count(d => d.IsReserved),
                DaysPresent = b.Days.Count(d => d.IsReserved && d.IsPresent),
                IsPastActivity = b.Activity.EndDate < today
            })
            .ToListAsync();

        var viewModel = new ChildViewModel
        {
            Id = child.Id,
            FirstName = child.FirstName,
            LastName = child.LastName,
            NationalRegisterNumber = child.NationalRegisterNumber,
            BirthDate = child.BirthDate,
            IsDisadvantagedEnvironment = child.IsDisadvantagedEnvironment,
            IsMildDisability = child.IsMildDisability,
            IsSevereDisability = child.IsSevereDisability,
            ParentId = child.ParentId,
            ParentFullName = parent != null ? $"{parent.FirstName} {parent.LastName}" : "",
            ActivityGroupId = child.ActivityGroupId,
            Bookings = bookings,

            // Audit fields
            CreatedAt = child.CreatedAt,
            CreatedBy = child.CreatedBy,
            ModifiedAt = child.ModifiedAt,
            ModifiedBy = child.ModifiedBy
        };

        // Fetch user display names for audit fields
        viewModel.CreatedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(child.CreatedBy);
        if (!string.IsNullOrEmpty(child.ModifiedBy))
        {
            viewModel.ModifiedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(child.ModifiedBy);
        }

        return viewModel;
    }


    private async Task PopulateParentDropdown(int? selectedParentId = null)
    {
        var parents = await _parentRepository.GetAllAsync();
        var parentList = parents
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Select(p => new
            {
                Id = p.Id,
                FullName = $"{p.FirstName} {p.LastName}"
            })
            .ToList();

        ViewBag.Parents = new SelectList(parentList, "Id", "FullName", selectedParentId);
    }
}
