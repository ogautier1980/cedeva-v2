using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Children.ViewModels;
using Cedeva.Infrastructure.Data;

namespace Cedeva.Website.Features.Children;

[Authorize]
public class ChildrenController : Controller
{
    private readonly IRepository<Child> _childRepository;
    private readonly IRepository<Parent> _parentRepository;
    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ChildrenController(
        IRepository<Child> childRepository,
        IRepository<Parent> parentRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork)
    {
        _childRepository = childRepository;
        _parentRepository = parentRepository;
        _context = context;
        _unitOfWork = unitOfWork;
    }

    // GET: Children
    public async Task<IActionResult> Index(string searchString, int? parentId, int pageNumber = 1, int pageSize = 10)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var allChildren = await _childRepository.GetAllAsync();
        var query = allChildren.AsQueryable();

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

        var totalItems = query.Count();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var children = query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
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
            .ToList();

        ViewData["SearchString"] = searchString;
        ViewData["ParentId"] = parentId;
        ViewData["PageNumber"] = pageNumber;
        ViewData["PageSize"] = pageSize;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalItems"] = totalItems;

        return View(children);
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

            TempData["SuccessMessage"] = "L'enfant a été créé avec succès.";
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

            TempData["SuccessMessage"] = "L'enfant a été modifié avec succès.";
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

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    // POST: Children/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var child = await _childRepository.GetByIdAsync(id);

        if (child == null)
        {
            return NotFound();
        }

        await _childRepository.DeleteAsync(child);
        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = "L'enfant a été supprimé avec succès.";
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
        var bookings = _context.Bookings
            .Where(b => b.ChildId == id)
            .Select(b => new BookingSummaryViewModel
            {
                Id = b.Id,
                ActivityName = b.Activity.Name,
                StartDate = b.Activity.StartDate,
                EndDate = b.Activity.EndDate,
                IsConfirmed = b.IsConfirmed
            })
            .ToList();

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
