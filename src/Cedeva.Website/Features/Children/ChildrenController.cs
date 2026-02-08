using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cedeva.Core.Entities;
using Cedeva.Core.Helpers;
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
    private const string SortOrderDescending = "desc";
    private const string SortOrderAscending = "asc";
    private const string SessionKeyChildrenSearchString = "Children_SearchString";
    private const string SessionKeyChildrenParentId = "Children_ParentId";
    private const string SessionKeyChildrenSortBy = "Children_SortBy";
    private const string SessionKeyChildrenSortOrder = "Children_SortOrder";
    private const string SessionKeyChildrenPageNumber = "Children_PageNumber";

    private readonly IRepository<Child> _childRepository;
    private readonly IRepository<Parent> _parentRepository;
    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExportFacadeService _exportServices;
    private readonly ICedevaControllerContext<ChildrenController> _ctx;

    public ChildrenController(
        IRepository<Child> childRepository,
        IRepository<Parent> parentRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IExportFacadeService exportServices,
        ICedevaControllerContext<ChildrenController> ctx)
    {
        _childRepository = childRepository;
        _parentRepository = parentRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _exportServices = exportServices;
        _ctx = ctx;
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
                _ctx.Session.Set("SessionKeyChildrenSearchString", queryParams.SearchString, persistToCookie: false);

            if (queryParams.ParentId.HasValue)
                _ctx.Session.Set("SessionKeyChildrenParentId", queryParams.ParentId, persistToCookie: false);

            if (!string.IsNullOrWhiteSpace(queryParams.SortBy))
                _ctx.Session.Set("SessionKeyChildrenSortBy", queryParams.SortBy, persistToCookie: false);

            if (!string.IsNullOrWhiteSpace(queryParams.SortOrder))
                _ctx.Session.Set("SessionKeyChildrenSortOrder", queryParams.SortOrder, persistToCookie: false);

            if (queryParams.PageNumber > 1)
                _ctx.Session.Set("SessionKeyChildrenPageNumber", queryParams.PageNumber.ToString(), persistToCookie: false);

            // Mark that filters should be kept for the next request (after redirect)
            TempData[ControllerExtensions.KeepFiltersKey] = true;

            // Redirect to clean URL
            return RedirectToAction(nameof(Index));
        }

        // If not keeping filters (no redirect, just navigation/F5), clear them
        if (TempData[ControllerExtensions.KeepFiltersKey] == null)
        {
            _ctx.Session.Clear("SessionKeyChildrenSearchString");
            _ctx.Session.Clear("SessionKeyChildrenParentId");
            _ctx.Session.Clear("SessionKeyChildrenSortBy");
            _ctx.Session.Clear("SessionKeyChildrenSortOrder");
            _ctx.Session.Clear("SessionKeyChildrenPageNumber");
        }

        // Load filters from state (will be empty if just cleared)
        queryParams.SearchString = _ctx.Session.Get("SessionKeyChildrenSearchString");
        queryParams.ParentId = _ctx.Session.Get<int>("SessionKeyChildrenParentId");
        queryParams.SortBy = _ctx.Session.Get("SessionKeyChildrenSortBy");
        queryParams.SortOrder = _ctx.Session.Get("SessionKeyChildrenSortOrder");

        var pageNumberStr = _ctx.Session.Get("SessionKeyChildrenPageNumber");
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
        query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortOrder?.ToLowerInvariant()) switch
        {
            ("firstname", SortOrderAscending) => query.OrderBy(c => c.FirstName).ThenBy(c => c.LastName),
            ("firstname", SortOrderDescending) => query.OrderByDescending(c => c.FirstName).ThenByDescending(c => c.LastName),
            ("lastname", SortOrderDescending) => query.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName),
            ("birthdate", SortOrderAscending) => query.OrderBy(c => c.BirthDate),
            ("birthdate", SortOrderDescending) => query.OrderByDescending(c => c.BirthDate),
            ("nationalregisternumber", SortOrderAscending) => query.OrderBy(c => c.NationalRegisterNumber),
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
            { _ctx.Localizer["Excel.FirstName"], c => c.FirstName },
            { _ctx.Localizer["Excel.LastName"], c => c.LastName },
            { _ctx.Localizer["Excel.NationalRegisterNumber"], c => c.NationalRegisterNumber },
            { _ctx.Localizer["Excel.BirthDate"], c => c.BirthDate },
            { _ctx.Localizer["Excel.Age"], c => DateTime.Today.Year - c.BirthDate.Year - (DateTime.Today.DayOfYear < c.BirthDate.DayOfYear ? 1 : 0) },
            { _ctx.Localizer["Excel.Parent"], c => c.Parent != null ? $"{c.Parent.FirstName} {c.Parent.LastName}" : "" },
            { _ctx.Localizer["Excel.ParentEmail"], c => c.Parent?.Email ?? "" },
            { _ctx.Localizer["Excel.ParentPhone"], c => c.Parent?.MobilePhoneNumber ?? c.Parent?.PhoneNumber ?? "" },
            { _ctx.Localizer["Excel.DisadvantagedEnvironment"], c => c.IsDisadvantagedEnvironment },
            { _ctx.Localizer["Excel.MildDisability"], c => c.IsMildDisability },
            { _ctx.Localizer["Excel.SevereDisability"], c => c.IsSevereDisability }
        };

        var sheetName = _ctx.Localizer["Excel.ChildrenSheet"];
        var excelData = _exportServices.Excel.ExportToExcel(children, sheetName, columns);
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
            { _ctx.Localizer["Excel.FirstName"], c => c.FirstName },
            { _ctx.Localizer["Excel.LastName"], c => c.LastName },
            { _ctx.Localizer["Excel.BirthDate"], c => c.BirthDate },
            { _ctx.Localizer["Excel.Age"], c => DateTime.Today.Year - c.BirthDate.Year - (DateTime.Today.DayOfYear < c.BirthDate.DayOfYear ? 1 : 0) },
            { _ctx.Localizer["Excel.Parent"], c => c.Parent != null ? $"{c.Parent.FirstName} {c.Parent.LastName}" : "" },
            { _ctx.Localizer["Excel.ParentPhone"], c => c.Parent?.MobilePhoneNumber ?? c.Parent?.PhoneNumber ?? "" }
        };

        var title = _ctx.Localizer["Excel.ChildrenSheet"];
        var pdfData = _exportServices.Pdf.ExportToPdf(children, title, columns);
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
        ViewBag.CurrentOrganisationId = _ctx.CurrentUser.OrganisationId ?? 0;

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
                NationalRegisterNumber = NationalRegisterNumberHelper.StripFormatting(viewModel.NationalRegisterNumber),
                BirthDate = viewModel.BirthDate,
                IsDisadvantagedEnvironment = viewModel.IsDisadvantagedEnvironment,
                IsMildDisability = viewModel.IsMildDisability,
                IsSevereDisability = viewModel.IsSevereDisability,
                ParentId = viewModel.ParentId,
                ActivityGroupId = viewModel.ActivityGroupId
            };

            await _childRepository.AddAsync(child);
            await _unitOfWork.SaveChangesAsync();

            TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.ChildCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = child.Id });
        }

        await PopulateParentDropdown(viewModel.ParentId);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAjax([FromForm] ChildViewModel viewModel)
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

        var child = new Child
        {
            FirstName = viewModel.FirstName,
            LastName = viewModel.LastName,
            NationalRegisterNumber = NationalRegisterNumberHelper.StripFormatting(viewModel.NationalRegisterNumber),
            BirthDate = viewModel.BirthDate,
            IsDisadvantagedEnvironment = viewModel.IsDisadvantagedEnvironment,
            IsMildDisability = viewModel.IsMildDisability,
            IsSevereDisability = viewModel.IsSevereDisability,
            ParentId = viewModel.ParentId,
            ActivityGroupId = viewModel.ActivityGroupId
        };

        await _childRepository.AddAsync(child);
        await _unitOfWork.SaveChangesAsync();

        _ctx.Logger.LogInformation("Child {Name} created via AJAX by user {UserId}", child.FullName, _ctx.CurrentUser.UserId);

        return Json(new
        {
            success = true,
            childId = child.Id,
            childName = child.FullName,
            message = _ctx.Localizer["Message.ChildCreated"].Value
        });
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
            NationalRegisterNumber = NationalRegisterNumberHelper.Format(child.NationalRegisterNumber),
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
            child.NationalRegisterNumber = NationalRegisterNumberHelper.StripFormatting(viewModel.NationalRegisterNumber);
            child.BirthDate = viewModel.BirthDate;
            child.IsDisadvantagedEnvironment = viewModel.IsDisadvantagedEnvironment;
            child.IsMildDisability = viewModel.IsMildDisability;
            child.IsSevereDisability = viewModel.IsSevereDisability;
            child.ParentId = viewModel.ParentId;
            child.ActivityGroupId = viewModel.ActivityGroupId;

            await _childRepository.UpdateAsync(child);
            await _unitOfWork.SaveChangesAsync();

            TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.ChildUpdated"].Value;
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

        TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.ChildDeleted"].Value;
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
        viewModel.CreatedByDisplayName = await _ctx.UserDisplay.GetUserDisplayNameAsync(child.CreatedBy);
        if (!string.IsNullOrEmpty(child.ModifiedBy))
        {
            viewModel.ModifiedByDisplayName = await _ctx.UserDisplay.GetUserDisplayNameAsync(child.ModifiedBy);
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
