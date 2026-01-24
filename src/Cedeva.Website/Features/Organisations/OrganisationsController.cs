using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Organisations.ViewModels;
using Cedeva.Infrastructure.Data;

namespace Cedeva.Website.Features.Organisations;

[Authorize(Roles = "Admin")]
public class OrganisationsController : Controller
{
    private readonly IRepository<Organisation> _organisationRepository;
    private readonly IRepository<Address> _addressRepository;
    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public OrganisationsController(
        IRepository<Organisation> organisationRepository,
        IRepository<Address> addressRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork)
    {
        _organisationRepository = organisationRepository;
        _addressRepository = addressRepository;
        _context = context;
        _unitOfWork = unitOfWork;
    }

    // GET: Organisations
    public async Task<IActionResult> Index(string searchString, int pageNumber = 1, int pageSize = 10)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var query = _context.Organisations
            .Include(o => o.Address)
            .Include(o => o.Activities)
            .Include(o => o.Parents)
            .Include(o => o.TeamMembers)
            .Include(o => o.Users)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(o =>
                o.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                o.Description.Contains(searchString, StringComparison.OrdinalIgnoreCase));
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var organisations = await query
            .OrderBy(o => o.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrganisationViewModel
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                LogoUrl = o.LogoUrl,
                Street = o.Address.Street,
                City = o.Address.City,
                PostalCode = o.Address.PostalCode,
                Country = o.Address.Country,
                AddressId = o.AddressId,
                ActivitiesCount = o.Activities.Count,
                ParentsCount = o.Parents.Count,
                TeamMembersCount = o.TeamMembers.Count,
                UsersCount = o.Users.Count
            })
            .ToListAsync();

        ViewData["SearchString"] = searchString;
        ViewData["PageNumber"] = pageNumber;
        ViewData["PageSize"] = pageSize;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalItems"] = totalItems;

        return View(organisations);
    }

    // GET: Organisations/Details/5
    public async Task<IActionResult> Details(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var organisation = await _organisationRepository.GetByIdAsync(id);

        if (organisation == null)
        {
            return NotFound();
        }

        var address = await _context.Addresses.FindAsync(organisation.AddressId);

        // Calculate counts asynchronously
        var activitiesCount = await _context.Activities.CountAsync(a => a.OrganisationId == id);
        var parentsCount = await _context.Parents.CountAsync(p => p.OrganisationId == id);
        var teamMembersCount = await _context.TeamMembers.CountAsync(t => t.OrganisationId == id);
        var usersCount = await _context.Users.CountAsync(u => u.OrganisationId == id);

        var viewModel = new OrganisationViewModel
        {
            Id = organisation.Id,
            Name = organisation.Name,
            Description = organisation.Description,
            LogoUrl = organisation.LogoUrl,
            Street = address?.Street ?? "",
            City = address?.City ?? "",
            PostalCode = address?.PostalCode ?? 0,
            Country = address?.Country ?? Core.Enums.Country.Belgium,
            AddressId = organisation.AddressId,
            ActivitiesCount = activitiesCount,
            ParentsCount = parentsCount,
            TeamMembersCount = teamMembersCount,
            UsersCount = usersCount
        };

        return View(viewModel);
    }

    // GET: Organisations/Create
    public IActionResult Create()
    {
        var viewModel = new OrganisationViewModel
        {
            Country = Core.Enums.Country.Belgium
        };

        return View(viewModel);
    }

    // POST: Organisations/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrganisationViewModel viewModel)
    {
        if (ModelState.IsValid)
        {
            // Create Address first
            var address = new Address
            {
                Street = viewModel.Street,
                City = viewModel.City,
                PostalCode = viewModel.PostalCode,
                Country = viewModel.Country
            };

            await _addressRepository.AddAsync(address);
            await _unitOfWork.SaveChangesAsync();

            // Create Organisation
            var organisation = new Organisation
            {
                Name = viewModel.Name,
                Description = viewModel.Description,
                LogoUrl = viewModel.LogoUrl,
                AddressId = address.Id
            };

            await _organisationRepository.AddAsync(organisation);
            await _unitOfWork.SaveChangesAsync();

            TempData["SuccessMessage"] = "L'organisation a été créée avec succès.";
            return RedirectToAction(nameof(Details), new { id = organisation.Id });
        }

        return View(viewModel);
    }

    // GET: Organisations/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var organisation = await _organisationRepository.GetByIdAsync(id);

        if (organisation == null)
        {
            return NotFound();
        }

        var address = await _context.Addresses.FindAsync(organisation.AddressId);

        var viewModel = new OrganisationViewModel
        {
            Id = organisation.Id,
            Name = organisation.Name,
            Description = organisation.Description,
            LogoUrl = organisation.LogoUrl,
            Street = address?.Street ?? "",
            City = address?.City ?? "",
            PostalCode = address?.PostalCode ?? 0,
            Country = address?.Country ?? Core.Enums.Country.Belgium,
            AddressId = organisation.AddressId
        };

        return View(viewModel);
    }

    // POST: Organisations/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OrganisationViewModel viewModel)
    {
        if (id != viewModel.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var organisation = await _organisationRepository.GetByIdAsync(id);

            if (organisation == null)
            {
                return NotFound();
            }

            // Update Address
            var address = await _context.Addresses.FindAsync(organisation.AddressId);
            if (address != null)
            {
                address.Street = viewModel.Street;
                address.City = viewModel.City;
                address.PostalCode = viewModel.PostalCode;
                address.Country = viewModel.Country;
                _context.Addresses.Update(address);
            }

            // Update Organisation
            organisation.Name = viewModel.Name;
            organisation.Description = viewModel.Description;
            organisation.LogoUrl = viewModel.LogoUrl;

            await _organisationRepository.UpdateAsync(organisation);
            await _unitOfWork.SaveChangesAsync();

            TempData["SuccessMessage"] = "L'organisation a été modifiée avec succès.";
            return RedirectToAction(nameof(Details), new { id = organisation.Id });
        }

        return View(viewModel);
    }

    // GET: Organisations/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var viewModel = await GetOrganisationViewModelWithStatsAsync(id);

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    // POST: Organisations/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var organisation = await _organisationRepository.GetByIdAsync(id);

        if (organisation == null)
        {
            return NotFound();
        }

        var addressId = organisation.AddressId;

        // Delete Organisation (cascade will handle related entities)
        await _organisationRepository.DeleteAsync(organisation);
        await _unitOfWork.SaveChangesAsync();

        // Delete Address
        var address = await _addressRepository.GetByIdAsync(addressId);
        if (address != null)
        {
            await _addressRepository.DeleteAsync(address);
            await _unitOfWork.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = "L'organisation a été supprimée avec succès.";
        return RedirectToAction(nameof(Index));
    }

    // Helper method to get organisation view model with statistics
    private async Task<OrganisationViewModel?> GetOrganisationViewModelWithStatsAsync(int id)
    {
        var organisation = await _organisationRepository.GetByIdAsync(id);

        if (organisation == null)
        {
            return null;
        }

        var address = await _context.Addresses.FindAsync(organisation.AddressId);

        // Calculate counts asynchronously
        var activitiesCount = await _context.Activities.CountAsync(a => a.OrganisationId == id);
        var parentsCount = await _context.Parents.CountAsync(p => p.OrganisationId == id);
        var teamMembersCount = await _context.TeamMembers.CountAsync(t => t.OrganisationId == id);
        var usersCount = await _context.Users.CountAsync(u => u.OrganisationId == id);

        return new OrganisationViewModel
        {
            Id = organisation.Id,
            Name = organisation.Name,
            Description = organisation.Description,
            LogoUrl = organisation.LogoUrl,
            Street = address?.Street ?? "",
            City = address?.City ?? "",
            PostalCode = address?.PostalCode ?? 0,
            Country = address?.Country ?? Core.Enums.Country.Belgium,
            AddressId = organisation.AddressId,
            ActivitiesCount = activitiesCount,
            ParentsCount = parentsCount,
            TeamMembersCount = teamMembersCount,
            UsersCount = usersCount
        };
    }
}
