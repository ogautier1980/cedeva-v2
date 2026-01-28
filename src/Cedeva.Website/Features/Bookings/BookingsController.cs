using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Bookings.ViewModels;
using Cedeva.Website.Localization;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Bookings;

[Authorize]
public class BookingsController : Controller
{
    private const string TempDataSuccessMessage = "SuccessMessage";
    private const string TempDataWarningMessage = "WarningMessage";

    private readonly IRepository<Booking> _bookingRepository;
    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelExportService _excelExportService;
    private readonly IEmailService _emailService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public BookingsController(
        IRepository<Booking> bookingRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IExcelExportService excelExportService,
        IEmailService emailService,
        IStringLocalizer<SharedResources> localizer)
    {
        _bookingRepository = bookingRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _excelExportService = excelExportService;
        _emailService = emailService;
        _localizer = localizer;
    }

    // GET: Bookings
    public async Task<IActionResult> Index(string? searchString, int? activityId, int? childId, bool? isConfirmed, int pageNumber = 1, int pageSize = 10)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var query = BuildBookingsQuery(_context, searchString, activityId, childId, isConfirmed);

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BookingViewModel
            {
                Id = b.Id,
                BookingDate = b.BookingDate,
                ChildId = b.ChildId,
                ActivityId = b.ActivityId,
                GroupId = b.GroupId,
                IsConfirmed = b.IsConfirmed,
                IsMedicalSheet = b.IsMedicalSheet,
                ChildFullName = b.Child != null ? b.Child.FirstName + " " + b.Child.LastName : "N/A",
                ParentFullName = b.Child != null && b.Child.Parent != null ? b.Child.Parent.FirstName + " " + b.Child.Parent.LastName : "N/A",
                ActivityName = b.Activity != null ? b.Activity.Name : "N/A",
                ActivityStartDate = b.Activity != null ? b.Activity.StartDate : DateTime.MinValue,
                ActivityEndDate = b.Activity != null ? b.Activity.EndDate : DateTime.MinValue,
                GroupLabel = b.Group != null ? b.Group.Label : null
            })
            .ToListAsync();

        ViewData["SearchString"] = searchString;
        ViewData["ActivityId"] = activityId;
        ViewData["ChildId"] = childId;
        ViewData["IsConfirmed"] = isConfirmed;
        ViewData["PageNumber"] = pageNumber;
        ViewData["PageSize"] = pageSize;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalItems"] = totalItems;

        return View(bookings);
    }

    private static IQueryable<Booking> BuildBookingsQuery(CedevaDbContext context, string? searchString, int? activityId, int? childId, bool? isConfirmed)
    {
        var query = context.Bookings
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .Include(b => b.Group)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(b =>
                b.Child.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                b.Child.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                b.Activity.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase));
        }

        if (activityId.HasValue)
        {
            query = query.Where(b => b.ActivityId == activityId.Value);
        }

        if (childId.HasValue)
        {
            query = query.Where(b => b.ChildId == childId.Value);
        }

        if (isConfirmed.HasValue)
        {
            query = query.Where(b => b.IsConfirmed == isConfirmed.Value);
        }

        return query;
    }

    // GET: Bookings/Details/5
    public async Task<IActionResult> Details(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var viewModel = await GetBookingViewModelAsync(id);

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    // GET: Bookings/Create
    public async Task<IActionResult> Create(int? childId, int? activityId)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await PopulateDropdowns(childId, activityId, null);

        var viewModel = new BookingViewModel
        {
            BookingDate = DateTime.Today,
            ChildId = childId ?? 0,
            ActivityId = activityId ?? 0,
            IsConfirmed = false,
            IsMedicalSheet = false
        };

        return View(viewModel);
    }

    // POST: Bookings/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingViewModel viewModel)
    {
        if (ModelState.IsValid)
        {
            var booking = new Booking
            {
                BookingDate = viewModel.BookingDate,
                ChildId = viewModel.ChildId,
                ActivityId = viewModel.ActivityId,
                GroupId = viewModel.GroupId,
                IsConfirmed = viewModel.IsConfirmed,
                IsMedicalSheet = viewModel.IsMedicalSheet
            };

            await _bookingRepository.AddAsync(booking);
            await _unitOfWork.SaveChangesAsync();

            TempData[TempDataSuccessMessage] = _localizer["Message.BookingCreated"];
            return RedirectToAction(nameof(Details), new { id = booking.Id });
        }

        await PopulateDropdowns(viewModel.ChildId, viewModel.ActivityId, viewModel.GroupId);
        return View(viewModel);
    }

    // GET: Bookings/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var booking = await _bookingRepository.GetByIdAsync(id);

        if (booking == null)
        {
            return NotFound();
        }

        var viewModel = new BookingViewModel
        {
            Id = booking.Id,
            BookingDate = booking.BookingDate,
            ChildId = booking.ChildId,
            ActivityId = booking.ActivityId,
            GroupId = booking.GroupId,
            IsConfirmed = booking.IsConfirmed,
            IsMedicalSheet = booking.IsMedicalSheet
        };

        await PopulateDropdowns(booking.ChildId, booking.ActivityId, booking.GroupId);
        return View(viewModel);
    }

    // POST: Bookings/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BookingViewModel viewModel)
    {
        if (id != viewModel.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var booking = await _bookingRepository.GetByIdAsync(id);

            if (booking == null)
            {
                return NotFound();
            }

            var wasNotConfirmed = !booking.IsConfirmed;

            booking.BookingDate = viewModel.BookingDate;
            booking.ChildId = viewModel.ChildId;
            booking.ActivityId = viewModel.ActivityId;
            booking.GroupId = viewModel.GroupId;
            booking.IsConfirmed = viewModel.IsConfirmed;
            booking.IsMedicalSheet = viewModel.IsMedicalSheet;

            await _bookingRepository.UpdateAsync(booking);
            await _unitOfWork.SaveChangesAsync();

            // Send confirmation email if booking was just confirmed
            if (wasNotConfirmed && booking.IsConfirmed)
            {
                await SendBookingConfirmationEmailAsync(booking);
            }
            else
            {
                TempData[TempDataSuccessMessage] = _localizer["Message.BookingUpdated"];
            }

            return RedirectToAction(nameof(Details), new { id = booking.Id });
        }

        await PopulateDropdowns(viewModel.ChildId, viewModel.ActivityId, viewModel.GroupId);
        return View(viewModel);
    }

    // GET: Bookings/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var viewModel = await GetBookingViewModelAsync(id);
        return viewModel == null ? NotFound() : View(viewModel);
    }

    // POST: Bookings/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        var booking = await _bookingRepository.GetByIdAsync(id);

        if (booking == null)
        {
            return NotFound();
        }

        await _bookingRepository.DeleteAsync(booking);
        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = _localizer["Message.BookingDeleted"];
        return RedirectToAction(nameof(Index));
    }

    // Helper method to get booking view model with all related data
    private async Task<BookingViewModel?> GetBookingViewModelAsync(int id)
    {
        var booking = await _bookingRepository.GetByIdAsync(id);

        if (booking == null)
        {
            return null;
        }

        var child = await _context.Children.FindAsync(booking.ChildId);
        var parent = child != null ? await _context.Parents.FindAsync(child.ParentId) : null;
        var activity = await _context.Activities.FindAsync(booking.ActivityId);
        var group = booking.GroupId.HasValue ? await _context.ActivityGroups.FindAsync(booking.GroupId.Value) : null;

        return new BookingViewModel
        {
            Id = booking.Id,
            BookingDate = booking.BookingDate,
            ChildId = booking.ChildId,
            ActivityId = booking.ActivityId,
            GroupId = booking.GroupId,
            IsConfirmed = booking.IsConfirmed,
            IsMedicalSheet = booking.IsMedicalSheet,
            ChildFullName = child != null ? $"{child.FirstName} {child.LastName}" : "",
            ParentFullName = parent != null ? $"{parent.FirstName} {parent.LastName}" : "",
            ActivityName = activity?.Name ?? "",
            ActivityStartDate = activity?.StartDate,
            ActivityEndDate = activity?.EndDate,
            GroupLabel = group?.Label,
            DaysCount = booking.Days.Count,
            QuestionAnswersCount = booking.QuestionAnswers.Count
        };
    }

    private async Task SendBookingConfirmationEmailAsync(Booking booking)
    {
        var child = await _context.Children
            .Include(c => c.Parent)
            .FirstOrDefaultAsync(c => c.Id == booking.ChildId);

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == booking.ActivityId);

        if (child?.Parent != null && activity != null)
        {
            try
            {
                await _emailService.SendBookingConfirmationEmailAsync(
                    child.Parent.Email,
                    $"{child.Parent.FirstName} {child.Parent.LastName}",
                    $"{child.FirstName} {child.LastName}",
                    activity.Name,
                    activity.StartDate,
                    activity.EndDate);

                TempData[TempDataSuccessMessage] = _localizer["Message.BookingConfirmedEmailSent"];
            }
            catch (Exception ex)
            {
                TempData[TempDataWarningMessage] = string.Format(_localizer["Message.BookingConfirmedEmailFailed"].Value, ex.Message);
            }
        }
        else
        {
            TempData[TempDataSuccessMessage] = _localizer["Message.BookingUpdated"];
        }
    }

    private async Task PopulateDropdowns(int? selectedChildId = null, int? selectedActivityId = null, int? selectedGroupId = null)
    {
        // Children dropdown
        var children = await _context.Children.ToListAsync();
        var childList = children
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Select(c => new
            {
                Id = c.Id,
                FullName = $"{c.FirstName} {c.LastName}"
            })
            .ToList();
        ViewBag.Children = new SelectList(childList, "Id", "FullName", selectedChildId);

        // Activities dropdown
        var activities = await _context.Activities.ToListAsync();
        var activityList = activities
            .OrderBy(a => a.StartDate)
            .ThenBy(a => a.Name)
            .Select(a => new
            {
                Id = a.Id,
                DisplayName = $"{a.Name} ({a.StartDate:dd/MM/yyyy} - {a.EndDate:dd/MM/yyyy})"
            })
            .ToList();
        ViewBag.Activities = new SelectList(activityList, "Id", "DisplayName", selectedActivityId);

        // Groups dropdown (optional)
        var groups = await _context.ActivityGroups.ToListAsync();
        var groupList = groups
            .OrderBy(g => g.Label)
            .Select(g => new
            {
                Id = g.Id,
                Label = g.Label
            })
            .ToList();
        ViewBag.Groups = new SelectList(groupList, "Id", "Label", selectedGroupId);
    }

    // GET: Bookings/GetGroupsByActivity
    [HttpGet]
    public async Task<IActionResult> GetGroupsByActivity(int activityId)
    {
        var groups = await _context.ActivityGroups
            .Where(g => g.ActivityId == activityId)
            .OrderBy(g => g.Label)
            .Select(g => new { id = g.Id, label = g.Label })
            .ToListAsync();

        return Json(groups);
    }

    // GET: Bookings/Export
    public async Task<IActionResult> Export(string? searchString, int? activityId, int? childId, bool? isConfirmed)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var allBookings = await _bookingRepository.GetAllAsync();
        var query = allBookings.AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(b =>
                b.Child.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                b.Child.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                b.Activity.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase));
        }

        if (activityId.HasValue)
        {
            query = query.Where(b => b.ActivityId == activityId.Value);
        }

        if (childId.HasValue)
        {
            query = query.Where(b => b.ChildId == childId.Value);
        }

        if (isConfirmed.HasValue)
        {
            query = query.Where(b => b.IsConfirmed == isConfirmed.Value);
        }

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();

        var columns = new Dictionary<string, Func<Booking, object>>
        {
            { _localizer["Excel.BookingDate"], b => b.BookingDate },
            { _localizer["Excel.Child"], b => $"{b.Child.FirstName} {b.Child.LastName}" },
            { _localizer["Excel.Parent"], b => $"{b.Child.Parent.FirstName} {b.Child.Parent.LastName}" },
            { _localizer["Excel.ParentEmail"], b => b.Child.Parent.Email },
            { _localizer["Excel.ParentPhone"], b => b.Child.Parent.MobilePhoneNumber ?? b.Child.Parent.PhoneNumber ?? "" },
            { _localizer["Excel.Activity"], b => b.Activity.Name },
            { _localizer["Excel.StartDate"], b => b.Activity.StartDate },
            { _localizer["Excel.EndDate"], b => b.Activity.EndDate },
            { _localizer["Excel.Group"], b => b.Group?.Label ?? "" },
            { _localizer["Excel.Confirmed"], b => b.IsConfirmed },
            { _localizer["Excel.MedicalSheet"], b => b.IsMedicalSheet }
        };

        var sheetName = _localizer["Excel.BookingsSheet"];
        var excelData = _excelExportService.ExportToExcel(bookings, sheetName, columns);
        var fileName = $"{sheetName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
