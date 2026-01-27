using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Bookings.ViewModels;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Bookings;

[Authorize]
public class BookingsController : Controller
{
    private readonly IRepository<Booking> _bookingRepository;
    private readonly IRepository<Child> _childRepository;
    private readonly IRepository<Activity> _activityRepository;
    private readonly IRepository<ActivityGroup> _activityGroupRepository;
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelExportService _excelExportService;
    private readonly IEmailService _emailService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public BookingsController(
        IRepository<Booking> bookingRepository,
        IRepository<Child> childRepository,
        IRepository<Activity> activityRepository,
        IRepository<ActivityGroup> activityGroupRepository,
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IExcelExportService excelExportService,
        IEmailService emailService,
        IStringLocalizer<SharedResources> localizer)
    {
        _bookingRepository = bookingRepository;
        _childRepository = childRepository;
        _activityRepository = activityRepository;
        _activityGroupRepository = activityGroupRepository;
        _context = context;
        _currentUserService = currentUserService;
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

        var query = _context.Bookings
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

        var totalItems = query.Count();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var bookings = query
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
            .ToList();

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

            TempData["SuccessMessage"] = _localizer["Message.BookingCreated"];
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

                        TempData["SuccessMessage"] = _localizer["Message.BookingConfirmedEmailSent"];
                    }
                    catch (Exception ex)
                    {
                        TempData["WarningMessage"] = $"L'inscription a été confirmée mais l'email n'a pas pu être envoyé: {ex.Message}";
                    }
                }
                else
                {
                    TempData["SuccessMessage"] = _localizer["Message.BookingUpdated"];
                }
            }
            else
            {
                TempData["SuccessMessage"] = _localizer["Message.BookingUpdated"];
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

        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    // POST: Bookings/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
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

    private async Task PopulateDropdowns(int? selectedChildId = null, int? selectedActivityId = null, int? selectedGroupId = null)
    {
        // Children dropdown
        var children = await _childRepository.GetAllAsync();
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
        var activities = await _activityRepository.GetAllAsync();
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
        var groups = await _activityGroupRepository.GetAllAsync();
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

        var bookings = query
            .OrderByDescending(b => b.BookingDate)
            .ToList();

        var columns = new Dictionary<string, Func<Booking, object>>
        {
            { "Date d'inscription", b => b.BookingDate },
            { "Enfant", b => $"{b.Child.FirstName} {b.Child.LastName}" },
            { "Parent", b => $"{b.Child.Parent.FirstName} {b.Child.Parent.LastName}" },
            { "Email parent", b => b.Child.Parent.Email },
            { "Téléphone parent", b => b.Child.Parent.MobilePhoneNumber ?? b.Child.Parent.PhoneNumber ?? "" },
            { "Activité", b => b.Activity.Name },
            { "Date début", b => b.Activity.StartDate },
            { "Date fin", b => b.Activity.EndDate },
            { "Groupe", b => b.Group?.Label ?? "" },
            { "Confirmé", b => b.IsConfirmed },
            { "Fiche médicale", b => b.IsMedicalSheet }
        };

        var excelData = _excelExportService.ExportToExcel(bookings, "Inscriptions", columns);
        var fileName = $"Inscriptions_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
