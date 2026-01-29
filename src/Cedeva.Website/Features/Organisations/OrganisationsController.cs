using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Organisations.ViewModels;
using Cedeva.Website.Localization;
using Cedeva.Infrastructure.Data;

namespace Cedeva.Website.Features.Organisations;

[Authorize(Roles = "Admin")]
public class OrganisationsController : Controller
{
    private const string TempDataSuccessMessage = "SuccessMessage";

    private readonly IRepository<Organisation> _organisationRepository;
    private readonly IRepository<Address> _addressRepository;
    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;

    public OrganisationsController(
        IRepository<Organisation> organisationRepository,
        IRepository<Address> addressRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IStringLocalizer<SharedResources> localizer,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService)
    {
        _organisationRepository = organisationRepository;
        _addressRepository = addressRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
    }

    // GET: Organisations
    public async Task<IActionResult> Index(string? searchString, string? sortBy = null, string? sortOrder = "asc", int pageNumber = 1, int pageSize = 10)
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

        // Apply sorting
        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("name", "desc") => query.OrderByDescending(o => o.Name),
            ("city", "asc") => query.OrderBy(o => o.Address.City),
            ("city", "desc") => query.OrderByDescending(o => o.Address.City),
            ("postalcode", "asc") => query.OrderBy(o => o.Address.PostalCode),
            ("postalcode", "desc") => query.OrderByDescending(o => o.Address.PostalCode),
            _ => query.OrderBy(o => o.Name) // default
        };

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var organisations = await query
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
        ViewData["SortBy"] = sortBy;
        ViewData["SortOrder"] = sortOrder;
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
        var childrenCount = await _context.Children.CountAsync(c => c.Parent != null && c.Parent.OrganisationId == id);
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
            PostalCode = address?.PostalCode ?? "",
            Country = address?.Country ?? Core.Enums.Country.Belgium,
            AddressId = organisation.AddressId,
            ActivitiesCount = activitiesCount,
            ParentsCount = parentsCount,
            ChildrenCount = childrenCount,
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

            TempData[TempDataSuccessMessage] = _localizer["Message.OrganisationCreated"];
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
            PostalCode = address?.PostalCode ?? "",
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

            TempData[TempDataSuccessMessage] = _localizer["Message.OrganisationUpdated"];
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

        TempData[TempDataSuccessMessage] = _localizer["Message.OrganisationDeleted"];
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
            PostalCode = address?.PostalCode ?? "",
            Country = address?.Country ?? Core.Enums.Country.Belgium,
            AddressId = organisation.AddressId,
            ActivitiesCount = activitiesCount,
            ParentsCount = parentsCount,
            TeamMembersCount = teamMembersCount,
            UsersCount = usersCount
        };
    }

    // GET: Organisations/Export
    public async Task<IActionResult> Export(string? searchString)
    {
        var query = _context.Organisations
            .Include(o => o.Address)
            .Include(o => o.Activities)
            .Include(o => o.Parents)
            .Include(o => o.TeamMembers)
            .Include(o => o.Users)
            .IgnoreQueryFilters()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            query = query.Where(o => o.Name.Contains(searchString) ||
                                     (o.Description != null && o.Description.Contains(searchString)));
        }

        var organisations = await query.OrderBy(o => o.Name).ToListAsync();

        var columns = new Dictionary<string, Func<Organisation, object>>
        {
            { _localizer["Excel.Name"], o => o.Name },
            { _localizer["Excel.Description"], o => o.Description ?? "" },
            { _localizer["Excel.Address"], o => $"{o.Address?.Street}, {o.Address?.PostalCode} {o.Address?.City}" },
            { _localizer["Excel.Activities"], o => o.Activities.Count },
            { _localizer["Excel.Parents"], o => o.Parents.Count },
            { _localizer["Excel.Team"], o => o.TeamMembers.Count },
            { _localizer["Excel.Users"], o => o.Users.Count }
        };

        var sheetName = _localizer["Excel.OrganisationsSheet"];
        var excelData = _excelExportService.ExportToExcel(organisations, sheetName, columns);
        var fileName = $"{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Organisations/ExportPdf
    public async Task<IActionResult> ExportPdf(string? searchString)
    {
        var query = _context.Organisations
            .Include(o => o.Address)
            .Include(o => o.Activities)
            .Include(o => o.Parents)
            .Include(o => o.TeamMembers)
            .Include(o => o.Users)
            .IgnoreQueryFilters()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            query = query.Where(o => o.Name.Contains(searchString) ||
                                     (o.Description != null && o.Description.Contains(searchString)));
        }

        var organisations = await query.OrderBy(o => o.Name).ToListAsync();

        var columns = new Dictionary<string, Func<Organisation, object>>
        {
            { _localizer["Excel.Name"], o => o.Name },
            { _localizer["Excel.Description"], o => o.Description ?? "" },
            { _localizer["Excel.Address"], o => $"{o.Address?.Street}, {o.Address?.PostalCode} {o.Address?.City}" },
            { _localizer["Excel.Activities"], o => o.Activities.Count },
            { _localizer["Excel.Parents"], o => o.Parents.Count },
            { _localizer["Excel.Team"], o => o.TeamMembers.Count },
            { _localizer["Excel.Users"], o => o.Users.Count }
        };

        var title = _localizer["Excel.OrganisationsSheet"];
        var pdfData = _pdfExportService.ExportToPdf(organisations, title, columns);
        var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(pdfData, "application/pdf", fileName);
    }
}
