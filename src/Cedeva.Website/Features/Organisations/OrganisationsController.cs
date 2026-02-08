using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Organisations.ViewModels;
using Cedeva.Website.Localization;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Infrastructure;

namespace Cedeva.Website.Features.Organisations;

[Authorize(Roles = "Admin")]
public class OrganisationsController : Controller
{
    private const string SortOrderDescending = "desc";

    private readonly IRepository<Organisation> _organisationRepository;
    private const string SessionKeyOrganisationsSearchString = "Organisations_SearchString";
    private const string SessionKeyOrganisationsSortBy = "Organisations_SortBy";
    private const string SessionKeyOrganisationsSortOrder = "Organisations_SortOrder";
    private const string SessionKeyOrganisationsPageNumber = "Organisations_PageNumber";
    private readonly IRepository<Address> _addressRepository;
    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IStorageService _storageService;
    private readonly IUserDisplayService _userDisplayService;
    private readonly ISessionStateService _sessionState;

    public OrganisationsController(
        IRepository<Organisation> organisationRepository,
        IRepository<Address> addressRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IStringLocalizer<SharedResources> localizer,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IStorageService storageService,
        IUserDisplayService userDisplayService,
        ISessionStateService sessionState)
    {
        _organisationRepository = organisationRepository;
        _addressRepository = addressRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _storageService = storageService;
        _userDisplayService = userDisplayService;
        _sessionState = sessionState;
    }

    // GET: Organisations
    public async Task<IActionResult> Index([FromQuery] OrganisationQueryParameters queryParams)
    {
        // Check if any query parameters were provided in the actual HTTP request
        bool hasQueryParams = Request.Query.Count > 0;

        // If query params provided, store them and redirect to clean URL
        if (hasQueryParams)
        {
            if (!string.IsNullOrWhiteSpace(queryParams.SearchString))
                _sessionState.Set(SessionKeyOrganisationsSearchString, queryParams.SearchString, persistToCookie: false);

            if (!string.IsNullOrWhiteSpace(queryParams.SortBy))
                _sessionState.Set(SessionKeyOrganisationsSortBy, queryParams.SortBy, persistToCookie: false);

            if (!string.IsNullOrWhiteSpace(queryParams.SortOrder))
                _sessionState.Set(SessionKeyOrganisationsSortOrder, queryParams.SortOrder, persistToCookie: false);

            if (queryParams.PageNumber > 1)
                _sessionState.Set(SessionKeyOrganisationsPageNumber, queryParams.PageNumber.ToString(), persistToCookie: false);

            // Mark that filters should be kept for the next request (after redirect)
            TempData[ControllerExtensions.KeepFiltersKey] = true;

            // Redirect to clean URL
            return RedirectToAction(nameof(Index));
        }

        // If not keeping filters (no redirect, just navigation/F5), clear them
        if (TempData[ControllerExtensions.KeepFiltersKey] == null)
        {
            _sessionState.Clear(SessionKeyOrganisationsSearchString);
            _sessionState.Clear(SessionKeyOrganisationsSortBy);
            _sessionState.Clear(SessionKeyOrganisationsSortOrder);
            _sessionState.Clear(SessionKeyOrganisationsPageNumber);
        }

        // Load filters from state (will be empty if just cleared)
        queryParams.SearchString = _sessionState.Get(SessionKeyOrganisationsSearchString);
        queryParams.SortBy = _sessionState.Get(SessionKeyOrganisationsSortBy);
        queryParams.SortOrder = _sessionState.Get(SessionKeyOrganisationsSortOrder);

        var pageNumberStr = _sessionState.Get(SessionKeyOrganisationsPageNumber);
        if (!string.IsNullOrEmpty(pageNumberStr) && int.TryParse(pageNumberStr, out var pageNum))
        {
            queryParams.PageNumber = pageNum;
        }

        var query = _context.Organisations
            .Include(o => o.Address)
            .Include(o => o.Activities)
            .Include(o => o.Parents)
            .Include(o => o.TeamMembers)
            .Include(o => o.Users)
            .AsQueryable();

        if (!string.IsNullOrEmpty(queryParams.SearchString))
        {
            query = query.Where(o =>
                o.Name.Contains(queryParams.SearchString, StringComparison.OrdinalIgnoreCase) ||
                o.Description.Contains(queryParams.SearchString, StringComparison.OrdinalIgnoreCase));
        }

        // Apply sorting
        query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortOrder?.ToLowerInvariant()) switch
        {
            ("name", SortOrderDescending) => query.OrderByDescending(o => o.Name),
            ("city", "asc") => query.OrderBy(o => o.Address.City),
            ("city", SortOrderDescending) => query.OrderByDescending(o => o.Address.City),
            ("postalcode", "asc") => query.OrderBy(o => o.Address.PostalCode),
            ("postalcode", SortOrderDescending) => query.OrderByDescending(o => o.Address.PostalCode),
            _ => query.OrderBy(o => o.Name) // default
        };

        var pagedResult = await query
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
            .ToPaginatedListAsync(queryParams.PageNumber, queryParams.PageSize);

        ViewData["SearchString"] = queryParams.SearchString;
        ViewData["SortBy"] = queryParams.SortBy;
        ViewData["SortOrder"] = queryParams.SortOrder;
        ViewData["PageNumber"] = pagedResult.PageNumber;
        ViewData["PageSize"] = pagedResult.PageSize;
        ViewData["TotalPages"] = pagedResult.TotalPages;
        ViewData["TotalItems"] = pagedResult.TotalItems;

        return View(pagedResult.Items);
    }

    // GET: Organisations/Details/5
    public async Task<IActionResult> Details(int id)
    {
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
            UsersCount = usersCount,

            // Audit fields
            CreatedAt = organisation.CreatedAt,
            CreatedBy = organisation.CreatedBy,
            ModifiedAt = organisation.ModifiedAt,
            ModifiedBy = organisation.ModifiedBy
        };

        // Fetch user display names for audit fields
        viewModel.CreatedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(organisation.CreatedBy);
        if (!string.IsNullOrEmpty(organisation.ModifiedBy))
        {
            viewModel.ModifiedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(organisation.ModifiedBy);
        }

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
            var address = await CreateAddressFromViewModel(viewModel);

            var organisation = new Organisation
            {
                Name = viewModel.Name,
                Description = viewModel.Description,
                AddressId = address.Id
            };

            await _organisationRepository.AddAsync(organisation);
            await _unitOfWork.SaveChangesAsync();

            await UploadLogoFileForNewOrganisation(organisation, viewModel.LogoFile);

            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.OrganisationCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = organisation.Id });
        }

        return View(viewModel);
    }

    // GET: Organisations/Edit/5
    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
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

        ViewData["ReturnUrl"] = returnUrl;
        return View(viewModel);
    }

    // POST: Organisations/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OrganisationViewModel viewModel, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

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

            await UpdateAddressFromViewModel(organisation.AddressId, viewModel);

            organisation.Name = viewModel.Name;
            organisation.Description = viewModel.Description;

            await HandleLogoRemoval(organisation, viewModel.RemoveLogo);
            await HandleLogoUpload(organisation, viewModel.LogoFile);

            await _organisationRepository.UpdateAsync(organisation);
            await _unitOfWork.SaveChangesAsync();

            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.OrganisationUpdated"].Value;
            return this.RedirectToReturnUrlOrAction(returnUrl, nameof(Details), new { id = organisation.Id });
        }

        return View(viewModel);
    }

    // POST: Organisations/DeleteLogo/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLogo(int id)
    {
        // Admin-only controller, no need for multi-tenancy check
        var organisation = await _organisationRepository.GetByIdAsync(id);

        if (organisation == null)
        {
            return NotFound();
        }

        // Delete logo file if exists
        if (!string.IsNullOrEmpty(organisation.LogoUrl))
        {
            try
            {
                await _storageService.DeleteFileAsync(organisation.LogoUrl);
            }
            catch
            {
                // Ignore errors if file doesn't exist
            }

            organisation.LogoUrl = null;
            await _unitOfWork.SaveChangesAsync();
        }

        return Ok();
    }

    // GET: Organisations/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
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

        // Delete logo file if exists
        if (!string.IsNullOrEmpty(organisation.LogoUrl))
        {
            try
            {
                await _storageService.DeleteFileAsync(organisation.LogoUrl);
            }
            catch
            {
                // Ignore errors if file doesn't exist
            }
        }

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

        TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Message.OrganisationDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    private async Task UpdateAddressFromViewModel(int addressId, OrganisationViewModel viewModel)
    {
        var address = await _context.Addresses.FindAsync(addressId);
        if (address != null)
        {
            address.Street = viewModel.Street;
            address.City = viewModel.City;
            address.PostalCode = viewModel.PostalCode;
            address.Country = viewModel.Country;
            _context.Addresses.Update(address);
        }
    }

    private async Task HandleLogoRemoval(Organisation organisation, bool removeLogo)
    {
        if (removeLogo && !string.IsNullOrEmpty(organisation.LogoUrl))
        {
            try
            {
                await _storageService.DeleteFileAsync(organisation.LogoUrl);
            }
            catch
            {
                // Ignore errors if file doesn't exist
            }
            organisation.LogoUrl = null;
        }
    }

    private async Task HandleLogoUpload(Organisation organisation, IFormFile? logoFile)
    {
        if (logoFile != null)
        {
            if (!string.IsNullOrEmpty(organisation.LogoUrl))
            {
                try
                {
                    await _storageService.DeleteFileAsync(organisation.LogoUrl);
                }
                catch
                {
                    // Ignore errors if file doesn't exist
                }
            }

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(logoFile.FileName)}";
            var filePath = await _storageService.UploadFileAsync(
                logoFile.OpenReadStream(),
                fileName,
                logoFile.ContentType,
                $"{organisation.Id}/logos"
            );
            organisation.LogoUrl = filePath;
        }
    }

    private async Task<Address> CreateAddressFromViewModel(OrganisationViewModel viewModel)
    {
        var address = new Address
        {
            Street = viewModel.Street,
            City = viewModel.City,
            PostalCode = viewModel.PostalCode,
            Country = viewModel.Country
        };

        await _addressRepository.AddAsync(address);
        await _unitOfWork.SaveChangesAsync();
        return address;
    }

    private async Task UploadLogoFileForNewOrganisation(Organisation organisation, IFormFile? logoFile)
    {
        if (logoFile != null)
        {
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(logoFile.FileName)}";
            var filePath = await _storageService.UploadFileAsync(
                logoFile.OpenReadStream(),
                fileName,
                logoFile.ContentType,
                $"{organisation.Id}/logos"
            );
            organisation.LogoUrl = filePath;
            await _unitOfWork.SaveChangesAsync();
        }
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
