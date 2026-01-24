using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Parents.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Website.Features.Parents;

[Authorize]
public class ParentsController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ParentsController> _logger;

    public ParentsController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        ILogger<ParentsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? searchTerm, int page = 1)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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

        var totalItems = await query.CountAsync();
        var pageSize = 10;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var parents = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync();

        var viewModel = new ParentListViewModel
        {
            Parents = parents,
            SearchTerm = searchTerm,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = pageSize
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var viewModel = await GetParentViewModelAsync(id);

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    public IActionResult Create()
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var viewModel = new ParentViewModel
        {
            Country = Country.Belgium,
            OrganisationId = _currentUserService.OrganisationId ?? 0
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ParentViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
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
            NationalRegisterNumber = viewModel.NationalRegisterNumber,
            Address = address,
            OrganisationId = _currentUserService.IsAdmin ? viewModel.OrganisationId : organisationId!.Value
        };

        _context.Parents.Add(parent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Parent {Name} created by user {UserId}", parent.FullName, _currentUserService.UserId);

        TempData["Success"] = "Le parent a été créé avec succès.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var parent = await _context.Parents
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (parent == null)
        {
            return NotFound();
        }

        var viewModel = MapToViewModel(parent);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ParentViewModel viewModel)
    {
        if (id != viewModel.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
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
        parent.NationalRegisterNumber = viewModel.NationalRegisterNumber;

        parent.Address.Street = viewModel.Street;
        parent.Address.City = viewModel.City;
        parent.Address.PostalCode = viewModel.PostalCode;
        parent.Address.Country = viewModel.Country;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Parent {Name} updated by user {UserId}", parent.FullName, _currentUserService.UserId);
            TempData["Success"] = "Le parent a été mis à jour avec succès.";
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ParentExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var viewModel = await GetParentViewModelAsync(id);

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
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
            TempData["Error"] = "Impossible de supprimer ce parent car il a des enfants enregistrés.";
            return RedirectToAction(nameof(Index));
        }

        _context.Addresses.Remove(parent.Address);
        _context.Parents.Remove(parent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Parent {Name} deleted by user {UserId}", parent.FullName, _currentUserService.UserId);
        TempData["Success"] = "Le parent a été supprimé avec succès.";

        return RedirectToAction(nameof(Index));
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

        return MapToViewModel(parent);
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
            NationalRegisterNumber = parent.NationalRegisterNumber,
            Street = parent.Address?.Street ?? string.Empty,
            City = parent.Address?.City ?? string.Empty,
            PostalCode = parent.Address?.PostalCode ?? 0,
            Country = parent.Address?.Country ?? Country.Belgium,
            AddressId = parent.AddressId,
            OrganisationId = parent.OrganisationId,
            ChildrenCount = parent.Children?.Count ?? 0,
            Children = parent.Children?.Select(c => new ChildSummaryViewModel
            {
                Id = c.Id,
                FullName = c.FullName,
                BirthDate = c.BirthDate
            }).ToList() ?? new List<ChildSummaryViewModel>()
        };
    }
}
