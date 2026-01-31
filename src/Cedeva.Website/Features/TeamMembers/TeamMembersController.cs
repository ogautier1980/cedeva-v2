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

    public TeamMembersController(
        IRepository<TeamMember> teamMemberRepository,
        IRepository<Address> addressRepository,
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IStringLocalizer<SharedResources> localizer)
    {
        _teamMemberRepository = teamMemberRepository;
        _addressRepository = addressRepository;
        _context = context;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _localizer = localizer;
    }

    // GET: TeamMembers
    public async Task<IActionResult> Index(string? searchString, string? sortBy = null, string? sortOrder = "asc", int pageNumber = 1, int pageSize = 10)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var query = _context.TeamMembers.AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(t =>
                t.FirstName.Contains(searchString) ||
                t.LastName.Contains(searchString) ||
                t.Email.Contains(searchString));
        }

        // Apply sorting
        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
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

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var teamMembers = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync();

        ViewData["SearchString"] = searchString;
        ViewData["SortBy"] = sortBy;
        ViewData["SortOrder"] = sortOrder;
        ViewData["PageNumber"] = pageNumber;
        ViewData["PageSize"] = pageSize;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalItems"] = totalItems;

        return View(teamMembers);
    }

    // GET: TeamMembers/Export
    public async Task<IActionResult> Export(string? searchString)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var viewModel = new TeamMemberViewModel
        {
            Country = Core.Enums.Country.Belgium,
            OrganisationId = _currentUserService.OrganisationId ?? 0,
            BirthDate = DateTime.Today.AddYears(-25) // Default age
        };

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

    // POST: TeamMembers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TeamMemberViewModel viewModel)
    {
        if (ModelState.IsValid)
        {
            // Determine OrganisationId based on user role
            var organisationId = _currentUserService.IsAdmin
                ? viewModel.OrganisationId
                : _currentUserService.OrganisationId ?? 0;

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

            // Create TeamMember
            var teamMember = new TeamMember
            {
                FirstName = viewModel.FirstName,
                LastName = viewModel.LastName,
                Email = viewModel.Email,
                MobilePhoneNumber = viewModel.MobilePhoneNumber,
                NationalRegisterNumber = viewModel.NationalRegisterNumber,
                BirthDate = viewModel.BirthDate,
                AddressId = address.Id,
                TeamRole = viewModel.TeamRole,
                License = viewModel.License,
                Status = viewModel.Status,
                DailyCompensation = viewModel.DailyCompensation,
                LicenseUrl = viewModel.LicenseUrl,
                OrganisationId = organisationId
            };

            await _teamMemberRepository.AddAsync(teamMember);
            await _unitOfWork.SaveChangesAsync();

            TempData[TempDataSuccessMessage] = _localizer["Message.TeamMemberCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = teamMember.TeamMemberId });
        }

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

    // GET: TeamMembers/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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

    // POST: TeamMembers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TeamMemberViewModel viewModel)
    {
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

            // Update Address
            var address = await _context.Addresses.FindAsync(teamMember.AddressId);
            if (address != null)
            {
                address.Street = viewModel.Street;
                address.City = viewModel.City;
                address.PostalCode = viewModel.PostalCode;
                address.Country = viewModel.Country;
                _context.Addresses.Update(address);
            }

            // Update TeamMember
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
            teamMember.LicenseUrl = viewModel.LicenseUrl;

            // Update OrganisationId if admin
            if (_currentUserService.IsAdmin)
            {
                teamMember.OrganisationId = viewModel.OrganisationId;
            }

            await _teamMemberRepository.UpdateAsync(teamMember);
            await _unitOfWork.SaveChangesAsync();

            TempData[TempDataSuccessMessage] = _localizer["Message.TeamMemberUpdated"].Value;
            return RedirectToAction(nameof(Details), new { id = teamMember.TeamMemberId });
        }

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

    // GET: TeamMembers/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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

        return new TeamMemberViewModel
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
            ExpensesCount = teamMember.Expenses.Count
        };
    }
}
