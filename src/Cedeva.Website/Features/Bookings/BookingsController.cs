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
    private readonly IPdfExportService _pdfExportService;
    private readonly IEmailService _emailService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;

    public BookingsController(
        IRepository<Booking> bookingRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IEmailService emailService,
        IStringLocalizer<SharedResources> localizer,
        ICurrentUserService currentUserService)
    {
        _bookingRepository = bookingRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _emailService = emailService;
        _localizer = localizer;
        _currentUserService = currentUserService;
    }

    // GET: Bookings
    public async Task<IActionResult> Index(string? searchString, int? activityId, int? childId, bool? isConfirmed, string? sortBy = null, string? sortOrder = "asc", int pageNumber = 1, int pageSize = 10)
    {
        var query = BuildBookingsQuery(searchString, activityId, childId, isConfirmed);

        // Apply sorting
        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("bookingdate", "asc") => query.OrderBy(b => b.BookingDate),
            ("bookingdate", "desc") => query.OrderByDescending(b => b.BookingDate),
            ("childname", "asc") => query.OrderBy(b => b.Child.LastName).ThenBy(b => b.Child.FirstName),
            ("childname", "desc") => query.OrderByDescending(b => b.Child.LastName).ThenByDescending(b => b.Child.FirstName),
            ("activityname", "asc") => query.OrderBy(b => b.Activity.Name),
            ("activityname", "desc") => query.OrderByDescending(b => b.Activity.Name),
            ("activitystartdate", "asc") => query.OrderBy(b => b.Activity.StartDate),
            ("activitystartdate", "desc") => query.OrderByDescending(b => b.Activity.StartDate),
            ("isconfirmed", "asc") => query.OrderBy(b => b.IsConfirmed),
            ("isconfirmed", "desc") => query.OrderByDescending(b => b.IsConfirmed),
            _ => query.OrderByDescending(b => b.BookingDate) // default
        };

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var bookings = await query
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
        ViewData["SortBy"] = sortBy;
        ViewData["SortOrder"] = sortOrder;
        ViewData["PageNumber"] = pageNumber;
        ViewData["PageSize"] = pageSize;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalItems"] = totalItems;

        return View(bookings);
    }

    private IQueryable<Booking> BuildBookingsQuery(string? searchString, int? activityId, int? childId, bool? isConfirmed)
    {
        // Use IgnoreQueryFilters to load all related entities (Child, Parent, Activity, Group)
        // even if they would normally be filtered by multi-tenancy.
        // We still filter the Bookings themselves by organisation.
        var query = _context.Bookings
            .IgnoreQueryFilters()
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .Include(b => b.Activity)
            .Include(b => b.Group)
            .AsQueryable();

        // Manually apply organisation filter
        if (!_currentUserService.IsAdmin)
        {
            var organisationId = _currentUserService.OrganisationId;
            query = query.Where(b => b.Activity.OrganisationId == organisationId);
        }

        if (!string.IsNullOrEmpty(searchString))
        {
            var searchLower = searchString.ToLower();
            query = query.Where(b =>
                b.Child.FirstName.ToLower().Contains(searchLower) ||
                b.Child.LastName.ToLower().Contains(searchLower) ||
                b.Activity.Name.ToLower().Contains(searchLower));
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
            // Get activity to calculate total amount
            var activity = await _context.Activities.FindAsync(viewModel.ActivityId);
            if (activity == null)
            {
                ModelState.AddModelError("", _localizer["Message.ActivityNotFound"].Value);
                await PopulateDropdowns(viewModel.ChildId, viewModel.ActivityId, viewModel.GroupId);
                return View(viewModel);
            }

            // Calculate total amount based on number of selected days
            var numberOfDays = viewModel.SelectedActivityDayIds?.Count ?? 0;
            var totalAmount = (activity.PricePerDay ?? 0) * numberOfDays;

            var booking = new Booking
            {
                BookingDate = viewModel.BookingDate,
                ChildId = viewModel.ChildId,
                ActivityId = viewModel.ActivityId,
                GroupId = viewModel.GroupId,
                IsConfirmed = viewModel.IsConfirmed,
                IsMedicalSheet = viewModel.IsMedicalSheet,
                TotalAmount = totalAmount,
                PaidAmount = 0,
                PaymentStatus = Core.Enums.PaymentStatus.NotPaid
            };

            await _bookingRepository.AddAsync(booking);
            await _unitOfWork.SaveChangesAsync();

            // Create BookingDay entries for selected days
            if (viewModel.SelectedActivityDayIds != null && viewModel.SelectedActivityDayIds.Any())
            {
                foreach (var activityDayId in viewModel.SelectedActivityDayIds)
                {
                    var bookingDay = new BookingDay
                    {
                        BookingId = booking.Id,
                        ActivityDayId = activityDayId,
                        IsReserved = true,
                        IsPresent = false
                    };
                    _context.BookingDays.Add(bookingDay);
                }
                await _context.SaveChangesAsync();
            }

            TempData[TempDataSuccessMessage] = _localizer["Message.BookingCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = booking.Id });
        }

        await PopulateDropdowns(viewModel.ChildId, viewModel.ActivityId, viewModel.GroupId);
        return View(viewModel);
    }

    // GET: Bookings/Edit/5
    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var booking = await _context.Bookings
            .Include(b => b.Activity)
                .ThenInclude(a => a.Days)
            .Include(b => b.Days)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
        {
            return NotFound();
        }

        ViewData["ReturnUrl"] = returnUrl;

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

        // Populate weekly days with booking day status
        viewModel.WeeklyDays = booking.Activity.Days
            .GroupBy(d => d.Week)
            .OrderBy(g => g.Key)
            .Select(g => new WeeklyBookingDaysViewModel
            {
                WeekNumber = g.Key ?? 1,
                WeekLabel = $"Semaine {g.Key}",
                StartDate = g.Min(d => d.DayDate),
                EndDate = g.Max(d => d.DayDate),
                Days = g.OrderBy(d => d.DayDate)
                    .Select(d =>
                    {
                        var bookingDay = booking.Days.FirstOrDefault(bd => bd.ActivityDayId == d.DayId);
                        return new BookingDayDisplayViewModel
                        {
                            ActivityDayId = d.DayId,
                            Date = d.DayDate,
                            Label = d.Label,
                            DayOfWeek = d.DayDate.DayOfWeek,
                            IsReserved = bookingDay?.IsReserved ?? false,
                            IsPresent = bookingDay?.IsPresent ?? false
                        };
                    })
                    .ToList()
            })
            .ToList();

        await PopulateDropdowns(booking.ChildId, booking.ActivityId, booking.GroupId);
        return View(viewModel);
    }

    // POST: Bookings/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BookingViewModel viewModel, string? returnUrl = null)
    {
        if (id != viewModel.Id)
        {
            return NotFound();
        }

        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var booking = await _context.Bookings
                .Include(b => b.Days)
                .FirstOrDefaultAsync(b => b.Id == id);

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

            // Update BookingDays based on selected activity day IDs
            if (viewModel.SelectedActivityDayIds != null)
            {
                var selectedDayIds = viewModel.SelectedActivityDayIds;
                var existingDayIds = booking.Days.Select(bd => bd.ActivityDayId).ToList();

                // Remove deselected days
                var daysToRemove = booking.Days.Where(bd => !selectedDayIds.Contains(bd.ActivityDayId)).ToList();
                foreach (var dayToRemove in daysToRemove)
                {
                    _context.BookingDays.Remove(dayToRemove);
                }

                // Add newly selected days
                var newDayIds = selectedDayIds.Except(existingDayIds).ToList();
                foreach (var newDayId in newDayIds)
                {
                    var newBookingDay = new BookingDay
                    {
                        BookingId = booking.Id,
                        ActivityDayId = newDayId,
                        IsReserved = true,
                        IsPresent = false
                    };
                    _context.BookingDays.Add(newBookingDay);
                }
            }

            await _bookingRepository.UpdateAsync(booking);
            await _unitOfWork.SaveChangesAsync();

            // Send confirmation email if booking was just confirmed
            if (wasNotConfirmed && booking.IsConfirmed)
            {
                await SendBookingConfirmationEmailAsync(booking);
            }
            else
            {
                TempData[TempDataSuccessMessage] = _localizer["Message.BookingUpdated"].Value;
            }

            // Redirect to return URL if provided, otherwise to Details
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Details), new { id = booking.Id });
        }

        await PopulateDropdowns(viewModel.ChildId, viewModel.ActivityId, viewModel.GroupId);
        return View(viewModel);
    }

    // GET: Bookings/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
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

        TempData["SuccessMessage"] = _localizer["Message.BookingDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    // Helper method to get booking view model with all related data
    private async Task<BookingViewModel?> GetBookingViewModelAsync(int id)
    {
        var booking = await _context.Bookings
            .Include(b => b.Days)
                .ThenInclude(d => d.ActivityDay)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
        {
            return null;
        }

        var child = await _context.Children.FindAsync(booking.ChildId);
        var parent = child != null ? await _context.Parents.FindAsync(child.ParentId) : null;
        var activity = await _context.Activities.FindAsync(booking.ActivityId);
        var group = booking.GroupId.HasValue ? await _context.ActivityGroups.FindAsync(booking.GroupId.Value) : null;

        // Group booking days by week
        var weeklyDays = booking.Days
            .Where(d => d.IsReserved && d.ActivityDay != null)
            .GroupBy(d => d.ActivityDay.Week ?? 0)
            .OrderBy(g => g.Key)
            .Select(g => new WeeklyBookingDaysViewModel
            {
                WeekNumber = g.Key,
                WeekLabel = $"Semaine {g.Key}",
                StartDate = g.Min(d => d.ActivityDay.DayDate),
                EndDate = g.Max(d => d.ActivityDay.DayDate),
                Days = g.OrderBy(d => d.ActivityDay.DayDate)
                    .Select(d => new BookingDayDisplayViewModel
                    {
                        ActivityDayId = d.ActivityDayId,
                        Date = d.ActivityDay.DayDate,
                        Label = d.ActivityDay.Label,
                        DayOfWeek = d.ActivityDay.DayDate.DayOfWeek,
                        IsReserved = d.IsReserved,
                        IsPresent = d.IsPresent
                    })
                    .ToList()
            })
            .ToList();

        var viewModel = new BookingViewModel
        {
            Id = booking.Id,
            BookingDate = booking.BookingDate,
            ChildId = booking.ChildId,
            ActivityId = booking.ActivityId,
            GroupId = booking.GroupId,
            IsConfirmed = booking.IsConfirmed,
            IsMedicalSheet = booking.IsMedicalSheet,
            TotalAmount = booking.TotalAmount,
            PaidAmount = booking.PaidAmount,
            PaymentStatus = booking.PaymentStatus,
            ChildFullName = child != null ? $"{child.FirstName} {child.LastName}" : "",
            ParentFullName = parent != null ? $"{parent.FirstName} {parent.LastName}" : "",
            ActivityName = activity?.Name ?? "",
            ActivityStartDate = activity?.StartDate,
            ActivityEndDate = activity?.EndDate,
            GroupLabel = group?.Label,
            DaysCount = booking.Days.Count,
            QuestionAnswersCount = booking.QuestionAnswers.Count,
            WeeklyDays = weeklyDays,

            // Audit fields
            CreatedAt = booking.CreatedAt,
            CreatedBy = booking.CreatedBy,
            ModifiedAt = booking.ModifiedAt,
            ModifiedBy = booking.ModifiedBy
        };

        // Fetch user display names for audit fields
        viewModel.CreatedByDisplayName = await GetUserDisplayNameAsync(booking.CreatedBy);
        if (!string.IsNullOrEmpty(booking.ModifiedBy))
        {
            viewModel.ModifiedByDisplayName = await GetUserDisplayNameAsync(booking.ModifiedBy);
        }

        return viewModel;
    }

    private async Task<string> GetUserDisplayNameAsync(string userId)
    {
        if (userId == "System")
        {
            return "System";
        }

        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync();

        return user != null
            ? $"{user.FirstName} {user.LastName}".Trim()
            : userId; // Fallback to ID if user not found
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

                TempData[TempDataSuccessMessage] = _localizer["Message.BookingConfirmedEmailSent"].Value;
            }
            catch (Exception ex)
            {
                TempData[TempDataWarningMessage] = string.Format(_localizer["Message.BookingConfirmedEmailFailed"].Value, ex.Message);
            }
        }
        else
        {
            TempData[TempDataSuccessMessage] = _localizer["Message.BookingUpdated"].Value;
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

    // GET: Bookings/GetActivityDays
    [HttpGet]
    public async Task<IActionResult> GetActivityDays(int activityId)
    {
        var activityDays = await _context.ActivityDays
            .Where(d => d.ActivityId == activityId && d.IsActive)
            .OrderBy(d => d.DayDate)
            .Select(d => new
            {
                activityDayId = d.DayId,
                date = d.DayDate,
                label = d.Label,
                dayOfWeek = d.DayDate.DayOfWeek,
                isWeekend = d.DayDate.DayOfWeek == DayOfWeek.Saturday || d.DayDate.DayOfWeek == DayOfWeek.Sunday,
                isSelected = d.DayDate.DayOfWeek != DayOfWeek.Saturday && d.DayDate.DayOfWeek != DayOfWeek.Sunday // Mon-Fri by default
            })
            .ToListAsync();

        return Json(activityDays);
    }

    // GET: Bookings/Export
    public async Task<IActionResult> Export(string? searchString, int? activityId, int? childId, bool? isConfirmed)
    {
        var allBookings = await _bookingRepository.GetAllAsync();
        var query = allBookings.AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            var searchLower = searchString.ToLower();
            query = query.Where(b =>
                b.Child.FirstName.ToLower().Contains(searchLower) ||
                b.Child.LastName.ToLower().Contains(searchLower) ||
                b.Activity.Name.ToLower().Contains(searchLower));
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

    // GET: Bookings/ExportPdf
    public async Task<IActionResult> ExportPdf(string? searchString, int? activityId, int? childId, bool? isConfirmed)
    {
        var allBookings = await _bookingRepository.GetAllAsync();
        var query = allBookings.AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            var searchLower = searchString.ToLower();
            query = query.Where(b =>
                b.Child.FirstName.ToLower().Contains(searchLower) ||
                b.Child.LastName.ToLower().Contains(searchLower) ||
                b.Activity.Name.ToLower().Contains(searchLower));
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

        var title = _localizer["Excel.BookingsSheet"];
        var pdfData = _pdfExportService.ExportToPdf(bookings, title, columns);
        var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(pdfData, "application/pdf", fileName);
    }
}
