using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.TeamMembers.ViewModels;
using Cedeva.Website.Localization;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Cedeva.Website.Infrastructure;

namespace Cedeva.Website.Features.TeamMembers;

[Authorize]
public class TeamMembersController : Controller
{
    private const string TempDataSuccessMessage = "SuccessMessage";

    private readonly IRepository<TeamMember> _teamMemberRepository;
    private readonly IRepository<Address> _addressRepository;
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IStorageService _storageService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IUserDisplayService _userDisplayService;

    public TeamMembersController(
        IRepository<TeamMember> teamMemberRepository,
        IRepository<Address> addressRepository,
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IStringLocalizer<SharedResources> localizer,
        IStorageService storageService,
        IWebHostEnvironment webHostEnvironment,
        IUserDisplayService userDisplayService)
    {
        _teamMemberRepository = teamMemberRepository;
        _addressRepository = addressRepository;
        _context = context;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _localizer = localizer;
        _storageService = storageService;
        _webHostEnvironment = webHostEnvironment;
        _userDisplayService = userDisplayService;
    }

    // GET: TeamMembers
    public async Task<IActionResult> Index([FromQuery] TeamMemberQueryParameters queryParams)
    {
        var query = _context.TeamMembers.AsQueryable();

        if (!string.IsNullOrEmpty(queryParams.SearchString))
        {
            query = query.Where(t =>
                t.FirstName.Contains(queryParams.SearchString) ||
                t.LastName.Contains(queryParams.SearchString) ||
                t.Email.Contains(queryParams.SearchString));
        }

        // Apply sorting
        query = (queryParams.SortBy?.ToLower(), queryParams.SortOrder?.ToLower()) switch
        {
            ("firstname", "asc") => query.OrderBy(t => t.FirstName).ThenBy(t => t.LastName),
            ("firstname", "desc") => query.OrderByDescending(t => t.FirstName).ThenByDescending(t => t.LastName),
            ("lastname", "desc") => query.OrderByDescending(t => t.LastName).ThenByDescending(t => t.FirstName),
            ("email", "asc") => query.OrderBy(t => t.Email),
            ("email", "desc") => query.OrderByDescending(t => t.Email),
            ("teamrole", "asc") => query.OrderBy(t => t.TeamRole),
            ("teamrole", "desc") => query.OrderByDescending(t => t.TeamRole),
            ("status", "asc") => query.OrderBy(t => t.Status),
            ("status", "desc") => query.OrderByDescending(t => t.Status),
            _ => query.OrderBy(t => t.LastName).ThenBy(t => t.FirstName) // default
        };

        var pagedResult = await query
            .Select(t => new TeamMemberViewModel
            {
                TeamMemberId = t.TeamMemberId,
                FirstName = t.FirstName,
                LastName = t.LastName,
                Email = t.Email,
                MobilePhoneNumber = t.MobilePhoneNumber,
                TeamRole = t.TeamRole,
                License = t.License,
                Status = t.Status,
                OrganisationId = t.OrganisationId
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

    // GET: TeamMembers/Export
    public async Task<IActionResult> Export(string? searchString)
    {
        var query = _context.TeamMembers
            .Include(t => t.Address)
            .Include(t => t.Organisation)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(t =>
                t.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                t.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                t.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase));
        }

        var teamMembers = await query
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .ToListAsync();

        var columns = new Dictionary<string, Func<TeamMember, object>>
        {
            { _localizer["Excel.FirstName"], t => t.FirstName },
            { _localizer["Excel.LastName"], t => t.LastName },
            { _localizer["Excel.Email"], t => t.Email },
            { _localizer["Excel.MobilePhone"], t => t.MobilePhoneNumber },
            { _localizer["Excel.NationalRegisterNumber"], t => t.NationalRegisterNumber },
            { _localizer["Excel.BirthDate"], t => t.BirthDate },
            { _localizer["Excel.Age"], t => DateTime.Today.Year - t.BirthDate.Year - (DateTime.Today.DayOfYear < t.BirthDate.DayOfYear ? 1 : 0) },
            { _localizer["Excel.Role"], t => t.TeamRole.ToString() },
            { _localizer["Excel.License"], t => t.License.ToString() },
            { _localizer["Excel.Status"], t => t.Status.ToString() },
            { _localizer["Excel.DailyCompensation"], t => t.DailyCompensation ?? 0m },
            { _localizer["Excel.Organisation"], t => t.Organisation?.Name ?? "" },
            { _localizer["Excel.Street"], t => t.Address?.Street ?? "" },
            { _localizer["Excel.PostalCode"], t => t.Address?.PostalCode ?? "" },
            { _localizer["Excel.City"], t => t.Address?.City ?? "" }
        };

        var sheetName = _localizer["Excel.TeamMembersSheet"];
        var excelData = _excelExportService.ExportToExcel(teamMembers, sheetName, columns);
        var fileName = $"{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: TeamMembers/ExportPdf
    public async Task<IActionResult> ExportPdf(string? searchString)
    {
        var query = _context.TeamMembers
            .Include(t => t.Address)
            .Include(t => t.Organisation)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(t =>
                t.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                t.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                t.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase));
        }

        var teamMembers = await query
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .ToListAsync();

        var columns = new Dictionary<string, Func<TeamMember, object>>
        {
            { _localizer["Excel.FirstName"], t => t.FirstName },
            { _localizer["Excel.LastName"], t => t.LastName },
            { _localizer["Excel.Email"], t => t.Email },
            { _localizer["Excel.MobilePhone"], t => t.MobilePhoneNumber },
            { _localizer["Excel.Role"], t => t.TeamRole.ToString() },
            { _localizer["Excel.License"], t => t.License.ToString() },
            { _localizer["Excel.Status"], t => t.Status.ToString() },
            { _localizer["Excel.City"], t => t.Address?.City ?? "" }
        };

        var title = _localizer["Excel.TeamMembersSheet"];
        var pdfData = _pdfExportService.ExportToPdf(teamMembers, title, columns);
        var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(pdfData, "application/pdf", fileName);
    }

    // GET: TeamMembers/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var viewModel = await GetTeamMemberViewModelAsync(id);

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    // GET: TeamMembers/Create
    public async Task<IActionResult> Create()
    {
        var viewModel = new TeamMemberViewModel
        {
            Country = Core.Enums.Country.Belgium,
            OrganisationId = _currentUserService.OrganisationId ?? 0,
            BirthDate = DateTime.Today.AddYears(-25) // Default age
        };

        await PopulateOrganisationsDropdown(viewModel.OrganisationId);

        return View(viewModel);
    }

    // POST: TeamMembers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TeamMemberViewModel viewModel)
    {
        if (ModelState.IsValid)
        {
            var organisationId = _currentUserService.IsAdmin
                ? viewModel.OrganisationId
                : _currentUserService.OrganisationId ?? 0;

            var address = await CreateAddressFromViewModel(viewModel);
            var teamMember = await CreateTeamMemberFromViewModel(viewModel, address.Id, organisationId);
            await UploadLicenseFileIfProvided(viewModel, teamMember, organisationId);

            TempData[TempDataSuccessMessage] = _localizer["Message.TeamMemberCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = teamMember.TeamMemberId });
        }

        await PopulateOrganisationsDropdown(viewModel.OrganisationId);
        return View(viewModel);
    }

    private async Task<Address> CreateAddressFromViewModel(TeamMemberViewModel viewModel)
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

    private async Task<TeamMember> CreateTeamMemberFromViewModel(TeamMemberViewModel viewModel, int addressId, int organisationId)
    {
        var teamMember = new TeamMember
        {
            FirstName = viewModel.FirstName,
            LastName = viewModel.LastName,
            Email = viewModel.Email,
            MobilePhoneNumber = viewModel.MobilePhoneNumber,
            NationalRegisterNumber = viewModel.NationalRegisterNumber,
            BirthDate = viewModel.BirthDate,
            AddressId = addressId,
            TeamRole = viewModel.TeamRole,
            License = viewModel.License,
            Status = viewModel.Status,
            DailyCompensation = viewModel.DailyCompensation,
            OrganisationId = organisationId
        };

        await _teamMemberRepository.AddAsync(teamMember);
        await _unitOfWork.SaveChangesAsync();
        return teamMember;
    }

    private async Task UploadLicenseFileIfProvided(TeamMemberViewModel viewModel, TeamMember teamMember, int organisationId)
    {
        if (viewModel.LicenseFile != null)
        {
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(viewModel.LicenseFile.FileName)}";
            var filePath = await _storageService.UploadFileAsync(
                viewModel.LicenseFile.OpenReadStream(),
                fileName,
                viewModel.LicenseFile.ContentType,
                $"{organisationId}/team-member-licenses"
            );
            teamMember.LicenseUrl = filePath;
            await _unitOfWork.SaveChangesAsync();
        }
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

    // GET: TeamMembers/Edit/5
    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var teamMember = await _teamMemberRepository.GetByIdAsync(id);

        if (teamMember == null)
        {
            return NotFound();
        }

        var address = await _context.Addresses.FindAsync(teamMember.AddressId);

        var viewModel = new TeamMemberViewModel
        {
            TeamMemberId = teamMember.TeamMemberId,
            FirstName = teamMember.FirstName,
            LastName = teamMember.LastName,
            Email = teamMember.Email,
            MobilePhoneNumber = teamMember.MobilePhoneNumber,
            NationalRegisterNumber = teamMember.NationalRegisterNumber,
            BirthDate = teamMember.BirthDate,
            Street = address?.Street ?? "",
            City = address?.City ?? "",
            PostalCode = address?.PostalCode ?? "",
            Country = address?.Country ?? Core.Enums.Country.Belgium,
            TeamRole = teamMember.TeamRole,
            License = teamMember.License,
            Status = teamMember.Status,
            DailyCompensation = teamMember.DailyCompensation,
            LicenseUrl = teamMember.LicenseUrl,
            AddressId = teamMember.AddressId,
            OrganisationId = teamMember.OrganisationId
        };

        await PopulateOrganisationsDropdown(viewModel.OrganisationId);

        ViewData["ReturnUrl"] = returnUrl;
        return View(viewModel);
    }

    // POST: TeamMembers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TeamMemberViewModel viewModel, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (id != viewModel.TeamMemberId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var teamMember = await _teamMemberRepository.GetByIdAsync(id);

            if (teamMember == null)
            {
                return NotFound();
            }

            await UpdateTeamMemberAddressAsync(teamMember.AddressId, viewModel);
            UpdateTeamMemberProperties(teamMember, viewModel);
            await HandleLicenseFileChangesAsync(teamMember, viewModel);

            await _teamMemberRepository.UpdateAsync(teamMember);
            await _unitOfWork.SaveChangesAsync();

            TempData[TempDataSuccessMessage] = _localizer["Message.TeamMemberUpdated"].Value;
            return this.RedirectToReturnUrlOrAction(returnUrl, nameof(Details), new { id = teamMember.TeamMemberId });
        }

        await PopulateOrganisationsDropdown(viewModel.OrganisationId);

        return View(viewModel);
    }

    // GET: TeamMembers/ViewLicense/5
    [HttpGet]
    public async Task<IActionResult> ViewLicense(int id)
    {
        // Bypass query filters to check multi-tenancy explicitly
        var teamMember = await _context.TeamMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(tm => tm.TeamMemberId == id);

        if (teamMember == null)
        {
            return NotFound();
        }

        // Multi-tenancy check
        if (!_currentUserService.IsAdmin)
        {
            var userOrgId = _currentUserService.OrganisationId;
            if (teamMember.OrganisationId != userOrgId)
            {
                return Forbid();
            }
        }

        if (string.IsNullOrEmpty(teamMember.LicenseUrl))
        {
            return NotFound();
        }

        // If local storage, return PhysicalFile
        if (teamMember.LicenseUrl.StartsWith("/uploads/"))
        {
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, teamMember.LicenseUrl.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var contentType = Path.GetExtension(filePath).ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

            return PhysicalFile(filePath, contentType);
        }
        else
        {
            // Azure Blob URL - redirect
            return Redirect(teamMember.LicenseUrl);
        }
    }

    // POST: TeamMembers/DeleteLicense/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLicense(int id)
    {
        // Bypass query filters to check multi-tenancy explicitly
        var teamMember = await _context.TeamMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(tm => tm.TeamMemberId == id);

        if (teamMember == null)
        {
            return NotFound();
        }

        // Multi-tenancy check
        if (!_currentUserService.IsAdmin)
        {
            var userOrgId = _currentUserService.OrganisationId;
            if (teamMember.OrganisationId != userOrgId)
            {
                return Forbid();
            }
        }

        // Delete license file if exists
        if (!string.IsNullOrEmpty(teamMember.LicenseUrl))
        {
            try
            {
                await _storageService.DeleteFileAsync(teamMember.LicenseUrl);
            }
            catch
            {
                // Ignore errors if file doesn't exist
            }

            teamMember.LicenseUrl = null;
            await _unitOfWork.SaveChangesAsync();
        }

        return Ok();
    }

    // GET: TeamMembers/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var viewModel = await GetTeamMemberViewModelAsync(id);
        return viewModel == null ? NotFound() : View(viewModel);
    }

    // POST: TeamMembers/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        var teamMember = await _teamMemberRepository.GetByIdAsync(id);

        if (teamMember == null)
        {
            return NotFound();
        }

        var addressId = teamMember.AddressId;

        // Delete license file if exists
        if (!string.IsNullOrEmpty(teamMember.LicenseUrl))
        {
            try
            {
                await _storageService.DeleteFileAsync(teamMember.LicenseUrl);
            }
            catch
            {
                // Ignore errors if file doesn't exist
            }
        }

        // Delete TeamMember
        await _teamMemberRepository.DeleteAsync(teamMember);
        await _unitOfWork.SaveChangesAsync();

        // Delete Address
        var address = await _addressRepository.GetByIdAsync(addressId);
        if (address != null)
        {
            await _addressRepository.DeleteAsync(address);
            await _unitOfWork.SaveChangesAsync();
        }

        TempData[TempDataSuccessMessage] = _localizer["Message.TeamMemberDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    // Helper method to get team member view model with all related data
    private async Task<TeamMemberViewModel?> GetTeamMemberViewModelAsync(int id)
    {
        var teamMember = await _teamMemberRepository.GetByIdAsync(id);

        if (teamMember == null)
        {
            return null;
        }

        var address = await _context.Addresses.FindAsync(teamMember.AddressId);

        var viewModel = new TeamMemberViewModel
        {
            TeamMemberId = teamMember.TeamMemberId,
            FirstName = teamMember.FirstName,
            LastName = teamMember.LastName,
            Email = teamMember.Email,
            MobilePhoneNumber = teamMember.MobilePhoneNumber,
            NationalRegisterNumber = teamMember.NationalRegisterNumber,
            BirthDate = teamMember.BirthDate,
            Street = address?.Street ?? "",
            City = address?.City ?? "",
            PostalCode = address?.PostalCode ?? "",
            Country = address?.Country ?? Core.Enums.Country.Belgium,
            TeamRole = teamMember.TeamRole,
            License = teamMember.License,
            Status = teamMember.Status,
            DailyCompensation = teamMember.DailyCompensation,
            LicenseUrl = teamMember.LicenseUrl,
            AddressId = teamMember.AddressId,
            OrganisationId = teamMember.OrganisationId,
            ActivitiesCount = teamMember.Activities.Count,
            ExpensesCount = teamMember.Expenses.Count,

            // Audit fields
            CreatedAt = teamMember.CreatedAt,
            CreatedBy = teamMember.CreatedBy,
            ModifiedAt = teamMember.ModifiedAt,
            ModifiedBy = teamMember.ModifiedBy
        };

        // Fetch user display names for audit fields
        viewModel.CreatedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(teamMember.CreatedBy);
        if (!string.IsNullOrEmpty(teamMember.ModifiedBy))
        {
            viewModel.ModifiedByDisplayName = await _userDisplayService.GetUserDisplayNameAsync(teamMember.ModifiedBy);
        }

        return viewModel;
    }


    /// <summary>
    /// Updates the team member's address with values from the view model.
    /// </summary>
    private async Task UpdateTeamMemberAddressAsync(int addressId, TeamMemberViewModel viewModel)
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

    /// <summary>
    /// Updates team member properties with values from the view model.
    /// </summary>
    private void UpdateTeamMemberProperties(TeamMember teamMember, TeamMemberViewModel viewModel)
    {
        teamMember.FirstName = viewModel.FirstName;
        teamMember.LastName = viewModel.LastName;
        teamMember.Email = viewModel.Email;
        teamMember.MobilePhoneNumber = viewModel.MobilePhoneNumber;
        teamMember.NationalRegisterNumber = viewModel.NationalRegisterNumber;
        teamMember.BirthDate = viewModel.BirthDate;
        teamMember.TeamRole = viewModel.TeamRole;
        teamMember.License = viewModel.License;
        teamMember.Status = viewModel.Status;
        teamMember.DailyCompensation = viewModel.DailyCompensation;

        if (_currentUserService.IsAdmin)
        {
            teamMember.OrganisationId = viewModel.OrganisationId;
        }
    }

    /// <summary>
    /// Handles license file removal and upload operations.
    /// </summary>
    private async Task HandleLicenseFileChangesAsync(TeamMember teamMember, TeamMemberViewModel viewModel)
    {
        // Handle license file removal
        if (viewModel.RemoveLicense && !string.IsNullOrEmpty(teamMember.LicenseUrl))
        {
            await DeleteLicenseFileAsync(teamMember.LicenseUrl);
            teamMember.LicenseUrl = null;
        }

        // Handle license file upload
        if (viewModel.LicenseFile != null)
        {
            // Delete old license if exists (and not already deleted)
            if (!string.IsNullOrEmpty(teamMember.LicenseUrl))
            {
                await DeleteLicenseFileAsync(teamMember.LicenseUrl);
            }

            // Upload new license
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(viewModel.LicenseFile.FileName)}";
            var filePath = await _storageService.UploadFileAsync(
                viewModel.LicenseFile.OpenReadStream(),
                fileName,
                viewModel.LicenseFile.ContentType,
                $"{teamMember.OrganisationId}/team-member-licenses"
            );
            teamMember.LicenseUrl = filePath;
        }
    }

    /// <summary>
    /// Safely deletes a license file, ignoring errors if file doesn't exist.
    /// </summary>
    private async Task DeleteLicenseFileAsync(string fileUrl)
    {
        try
        {
            await _storageService.DeleteFileAsync(fileUrl);
        }
        catch
        {
            // Ignore errors if file doesn't exist
        }
    }
}
