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

namespace Cedeva.Website.Features.Children;

[Authorize]
public class ChildrenController : Controller
{
    private const string TempDataSuccessMessage = "SuccessMessage";

    private readonly IRepository<Child> _childRepository;
    private readonly IRepository<Parent> _parentRepository;
    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ChildrenController(
        IRepository<Child> childRepository,
        IRepository<Parent> parentRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IStringLocalizer<SharedResources> localizer)
    {
        _childRepository = childRepository;
        _parentRepository = parentRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _localizer = localizer;
    }

    // GET: Children
    public async Task<IActionResult> Index(string? searchString, int? parentId, string? sortBy = null, string? sortOrder = "asc", int pageNumber = 1, int pageSize = 10)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var query = _context.Children.AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(c =>
                c.FirstName.Contains(searchString) ||
                c.LastName.Contains(searchString) ||
                c.NationalRegisterNumber.Contains(searchString));
        }

        if (parentId.HasValue)
        {
            query = query.Where(c => c.ParentId == parentId.Value);
        }

        // Apply sorting
        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("firstname", "asc") => query.OrderBy(c => c.FirstName).ThenBy(c => c.LastName),
            ("firstname", "desc") => query.OrderByDescending(c => c.FirstName).ThenByDescending(c => c.LastName),
            ("lastname", "desc") => query.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName),
            ("birthdate", "asc") => query.OrderBy(c => c.BirthDate),
            ("birthdate", "desc") => query.OrderByDescending(c => c.BirthDate),
            ("nationalregisternumber", "asc") => query.OrderBy(c => c.NationalRegisterNumber),
            ("nationalregisternumber", "desc") => query.OrderByDescending(c => c.NationalRegisterNumber),
            _ => query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName) // default
        };

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var children = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync();

        ViewData["SearchString"] = searchString;
        ViewData["ParentId"] = parentId;
        ViewData["SortBy"] = sortBy;
        ViewData["SortOrder"] = sortOrder;
        ViewData["PageNumber"] = pageNumber;
        ViewData["PageSize"] = pageSize;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalItems"] = totalItems;

        return View(children);
    }

    // GET: Children/Export
    public async Task<IActionResult> Export(string? searchString, int? parentId)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await PopulateParentDropdown(parentId);

        var viewModel = new ChildViewModel
        {
            ParentId = parentId ?? 0,
            BirthDate = DateTime.Today.AddYears(-10) // Default date
        };

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
    public async Task<IActionResult> Edit(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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

        await PopulateParentDropdown(child.ParentId);
        return View(viewModel);
    }

    // POST: Children/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ChildViewModel viewModel)
    {
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
            return RedirectToAction(nameof(Details), new { id = child.Id });
        }

        await PopulateParentDropdown(viewModel.ParentId);
        return View(viewModel);
    }

    // GET: Children/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
        var bookings = await _context.Bookings
            .Where(b => b.ChildId == id)
            .Select(b => new BookingSummaryViewModel
            {
                Id = b.Id,
                ActivityName = b.Activity.Name,
                StartDate = b.Activity.StartDate,
                EndDate = b.Activity.EndDate,
                IsConfirmed = b.IsConfirmed
            })
            .ToListAsync();

        return new ChildViewModel
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
            Bookings = bookings
        };
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
