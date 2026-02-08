using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Bookings.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Bookings;

[Authorize]
public class BookingsController : Controller
{
    private const string SortOrderDescending = "desc";
    private const string SessionKeyActivityId = "ActivityId";
    private const string SessionKeyBookingsSearchString = "Bookings_SearchString";
    private const string SessionKeyBookingsChildId = "Bookings_ChildId";
    private const string SessionKeyBookingsIsConfirmed = "Bookings_IsConfirmed";
    private const string SessionKeyBookingsSortBy = "Bookings_SortBy";
    private const string SessionKeyBookingsSortOrder = "Bookings_SortOrder";
    private const string SessionKeyBookingsPageNumber = "Bookings_PageNumber";

    private readonly IRepository<Booking> _bookingRepository;
    private readonly CedevaDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExportFacadeService _exportServices;
    private readonly IEmailService _emailService;
    private readonly IBookingQuestionService _bookingQuestionService;
    private readonly ICedevaControllerContext<BookingsController> _ctx;

    public BookingsController(
        IRepository<Booking> bookingRepository,
        CedevaDbContext context,
        IUnitOfWork unitOfWork,
        IExportFacadeService exportServices,
        IEmailService emailService,
        IBookingQuestionService bookingQuestionService,
        ICedevaControllerContext<BookingsController> ctx)
    {
        _bookingRepository = bookingRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _exportServices = exportServices;
        _emailService = emailService;
        _bookingQuestionService = bookingQuestionService;
        _ctx = ctx;
    }

    // GET: Bookings
    public async Task<IActionResult> Index([FromQuery] BookingQueryParameters queryParams)
    {
        if (Request.Query.Count > 0)
        {
            StoreBookingFiltersToSession(queryParams);
            TempData[ControllerExtensions.KeepFiltersKey] = true;
            return RedirectToAction(nameof(Index));
        }

        if (TempData[ControllerExtensions.KeepFiltersKey] == null)
        {
            ClearBookingFilters();
        }

        LoadBookingFiltersFromSession(queryParams);

        var query = BuildBookingsQuery(
            queryParams.SearchString,
            queryParams.ActivityId,
            queryParams.ChildId,
            queryParams.IsConfirmed);

        // Apply sorting
        query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortOrder?.ToLowerInvariant()) switch
        {
            ("bookingdate", "asc") => query.OrderBy(b => b.BookingDate),
            ("bookingdate", SortOrderDescending) => query.OrderByDescending(b => b.BookingDate),
            ("childname", "asc") => query.OrderBy(b => b.Child.LastName).ThenBy(b => b.Child.FirstName),
            ("childname", SortOrderDescending) => query.OrderByDescending(b => b.Child.LastName).ThenByDescending(b => b.Child.FirstName),
            ("activityname", "asc") => query.OrderBy(b => b.Activity.Name),
            ("activityname", SortOrderDescending) => query.OrderByDescending(b => b.Activity.Name),
            ("activitystartdate", "asc") => query.OrderBy(b => b.Activity.StartDate),
            ("activitystartdate", SortOrderDescending) => query.OrderByDescending(b => b.Activity.StartDate),
            ("isconfirmed", "asc") => query.OrderBy(b => b.IsConfirmed),
            ("isconfirmed", SortOrderDescending) => query.OrderByDescending(b => b.IsConfirmed),
            _ => query.OrderByDescending(b => b.BookingDate) // default
        };

        var pagedResult = await query
            .Select(b => new BookingViewModel
            {
                Id = b.Id,
                BookingDate = b.BookingDate,
                ChildId = b.ChildId,
                ActivityId = b.ActivityId,
                GroupId = b.GroupId,
                IsConfirmed = b.IsConfirmed,
                IsMedicalSheet = b.IsMedicalSheet,
                ChildFullName = b.Child != null ? $"{b.Child.FirstName} {b.Child.LastName}" : "N/A",
                ParentFullName = b.Child != null && b.Child.Parent != null ? $"{b.Child.Parent.FirstName} {b.Child.Parent.LastName}" : "N/A",
                ActivityName = b.Activity != null ? b.Activity.Name : "N/A",
                ActivityStartDate = b.Activity != null ? b.Activity.StartDate : DateTime.MinValue,
                ActivityEndDate = b.Activity != null ? b.Activity.EndDate : DateTime.MinValue,
                GroupLabel = b.Group != null ? b.Group.Label : null
            })
            .ToPaginatedListAsync(queryParams.PageNumber, queryParams.PageSize);

        ViewData["SearchString"] = queryParams.SearchString;
        ViewData["ActivityId"] = queryParams.ActivityId;
        ViewData["ChildId"] = queryParams.ChildId;
        ViewData["IsConfirmed"] = queryParams.IsConfirmed;
        ViewData["SortBy"] = queryParams.SortBy;
        ViewData["SortOrder"] = queryParams.SortOrder;
        ViewData["PageNumber"] = pagedResult.PageNumber;
        ViewData["PageSize"] = pagedResult.PageSize;
        ViewData["TotalPages"] = pagedResult.TotalPages;
        ViewData["TotalItems"] = pagedResult.TotalItems;

        return View(pagedResult.Items);
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
        if (!_ctx.CurrentUser.IsAdmin)
        {
            var organisationId = _ctx.CurrentUser.OrganisationId;
            query = query.Where(b => b.Activity.OrganisationId == organisationId);
        }

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

        // Pass organisation ID for inline parent creation
        ViewBag.CurrentOrganisationId = _ctx.CurrentUser.OrganisationId ?? 0;

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
                ModelState.AddModelError("", _ctx.Localizer["Message.ActivityNotFound"].Value);
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

            // Save question answers
            if (viewModel.QuestionAnswers != null && viewModel.QuestionAnswers.Any())
            {
                await _bookingQuestionService.SaveAnswersAsync(booking.Id, viewModel.QuestionAnswers);
            }

            TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.BookingCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = booking.Id });
        }

        await PopulateDropdowns(viewModel.ChildId, viewModel.ActivityId, viewModel.GroupId);
        return View(viewModel);
    }

    // GET: Bookings/GetActivityQuestions?activityId=5
    [HttpGet]
    public async Task<IActionResult> GetActivityQuestions(int activityId)
    {
        var questions = await _context.ActivityQuestions
            .Where(q => q.ActivityId == activityId && q.IsActive)
            .OrderBy(q => q.DisplayOrder)
            .Select(q => new
            {
                id = q.Id,
                questionText = q.QuestionText,
                questionType = (int)q.QuestionType,
                isRequired = q.IsRequired,
                options = q.Options,
                displayOrder = q.DisplayOrder
            })
            .ToListAsync();

        return Json(new { questions });
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

        // Load questions and existing answers
        var questionDtos = await _bookingQuestionService.GetQuestionsWithAnswersAsync(booking.ActivityId, id);

        viewModel.Questions = questionDtos.Select(dto => new BookingQuestionViewModel
        {
            Id = dto.Id,
            QuestionText = dto.QuestionText,
            QuestionType = dto.QuestionType,
            IsRequired = dto.IsRequired,
            Options = dto.Options,
            DisplayOrder = dto.DisplayOrder,
            AnswerText = dto.AnswerText
        }).ToList();

        // Pre-fill QuestionAnswers dictionary for form binding
        viewModel.QuestionAnswers = viewModel.Questions
            .Where(q => !string.IsNullOrEmpty(q.AnswerText))
            .ToDictionary(q => q.Id, q => q.AnswerText!);

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

            UpdateBookingDays(booking, viewModel.SelectedActivityDayIds);

            await _bookingRepository.UpdateAsync(booking);
            await _unitOfWork.SaveChangesAsync();

            // Update question answers
            if (viewModel.QuestionAnswers != null)
            {
                await _bookingQuestionService.SaveAnswersAsync(id, viewModel.QuestionAnswers);
            }

            if (wasNotConfirmed && booking.IsConfirmed)
            {
                await SendBookingConfirmationEmailAsync(booking);
            }
            else
            {
                TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.BookingUpdated"].Value;
            }

            return this.RedirectToReturnUrlOrAction(returnUrl, nameof(Details), new { id = booking.Id });
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

        TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.BookingDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    private void UpdateBookingDays(Booking booking, List<int>? selectedActivityDayIds)
    {
        if (selectedActivityDayIds == null)
            return;

        var existingDayIds = booking.Days.Select(bd => bd.ActivityDayId).ToList();

        var daysToRemove = booking.Days.Where(bd => !selectedActivityDayIds.Contains(bd.ActivityDayId)).ToList();
        foreach (var dayToRemove in daysToRemove)
        {
            _context.BookingDays.Remove(dayToRemove);
        }

        var newDayIds = selectedActivityDayIds.Except(existingDayIds).ToList();
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

    // Helper method to get booking view model with all related data
    private async Task<BookingViewModel?> GetBookingViewModelAsync(int id)
    {
        var booking = await _context.Bookings
            .Include(b => b.Days)
                .ThenInclude(d => d.ActivityDay)
            .Include(b => b.QuestionAnswers)
                .ThenInclude(qa => qa.ActivityQuestion)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
        {
            return null;
        }

        var child = await _context.Children.FindAsync(booking.ChildId);
        var parent = child != null ? await _context.Parents.FindAsync(child.ParentId) : null;
        var activity = await _context.Activities.FindAsync(booking.ActivityId);
        var group = booking.GroupId.HasValue ? await _context.ActivityGroups.FindAsync(booking.GroupId.Value) : null;

        // Load questions with answers
        var questionDtos = await _bookingQuestionService.GetQuestionsWithAnswersAsync(booking.ActivityId, id);

        var questions = questionDtos.Select(dto => new BookingQuestionViewModel
        {
            Id = dto.Id,
            QuestionText = dto.QuestionText,
            QuestionType = dto.QuestionType,
            IsRequired = dto.IsRequired,
            Options = dto.Options,
            DisplayOrder = dto.DisplayOrder,
            AnswerText = dto.AnswerText
        }).ToList();

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
            Questions = questions,

            // Audit fields
            CreatedAt = booking.CreatedAt,
            CreatedBy = booking.CreatedBy,
            ModifiedAt = booking.ModifiedAt,
            ModifiedBy = booking.ModifiedBy
        };

        // Fetch user display names for audit fields
        viewModel.CreatedByDisplayName = await _ctx.UserDisplay.GetUserDisplayNameAsync(booking.CreatedBy);
        if (!string.IsNullOrEmpty(booking.ModifiedBy))
        {
            viewModel.ModifiedByDisplayName = await _ctx.UserDisplay.GetUserDisplayNameAsync(booking.ModifiedBy);
        }

        return viewModel;
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

                TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.BookingConfirmedEmailSent"].Value;
            }
            catch (Exception ex)
            {
                _ctx.Logger.LogWarning(ex, "Failed to send booking confirmation email to {Email} for booking {BookingId}",
                    child.Parent.Email, booking.Id);
                TempData[ControllerExtensions.WarningMessageKey] = string.Format(_ctx.Localizer["Message.BookingConfirmedEmailFailed"].Value, ex.Message);
            }
        }
        else
        {
            TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.BookingUpdated"].Value;
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

        // Parents dropdown (for inline child creation)
        var parents = await _context.Parents.ToListAsync();
        var parentList = parents
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Select(p => new
            {
                Id = p.Id,
                FullName = $"{p.FirstName} {p.LastName}"
            })
            .ToList();
        ViewBag.Parents = new SelectList(parentList, "Id", "FullName");

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
                week = d.Week ?? 0,
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
            { _ctx.Localizer["Excel.BookingDate"], b => b.BookingDate },
            { _ctx.Localizer["Excel.Child"], b => $"{b.Child.FirstName} {b.Child.LastName}" },
            { _ctx.Localizer["Excel.Parent"], b => $"{b.Child.Parent.FirstName} {b.Child.Parent.LastName}" },
            { _ctx.Localizer["Excel.ParentEmail"], b => b.Child.Parent.Email },
            { _ctx.Localizer["Excel.ParentPhone"], b => b.Child.Parent.MobilePhoneNumber ?? b.Child.Parent.PhoneNumber ?? "" },
            { _ctx.Localizer["Excel.Activity"], b => b.Activity.Name },
            { _ctx.Localizer["Excel.StartDate"], b => b.Activity.StartDate },
            { _ctx.Localizer["Excel.EndDate"], b => b.Activity.EndDate },
            { _ctx.Localizer["Excel.Group"], b => b.Group?.Label ?? "" },
            { _ctx.Localizer["Excel.Confirmed"], b => b.IsConfirmed },
            { _ctx.Localizer["Excel.MedicalSheet"], b => b.IsMedicalSheet }
        };

        var sheetName = _ctx.Localizer["Excel.BookingsSheet"];
        var excelData = _exportServices.Excel.ExportToExcel(bookings, sheetName, columns);
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
            { _ctx.Localizer["Excel.BookingDate"], b => b.BookingDate },
            { _ctx.Localizer["Excel.Child"], b => $"{b.Child.FirstName} {b.Child.LastName}" },
            { _ctx.Localizer["Excel.Parent"], b => $"{b.Child.Parent.FirstName} {b.Child.Parent.LastName}" },
            { _ctx.Localizer["Excel.ParentEmail"], b => b.Child.Parent.Email },
            { _ctx.Localizer["Excel.ParentPhone"], b => b.Child.Parent.MobilePhoneNumber ?? b.Child.Parent.PhoneNumber ?? "" },
            { _ctx.Localizer["Excel.Activity"], b => b.Activity.Name },
            { _ctx.Localizer["Excel.StartDate"], b => b.Activity.StartDate },
            { _ctx.Localizer["Excel.EndDate"], b => b.Activity.EndDate },
            { _ctx.Localizer["Excel.Group"], b => b.Group?.Label ?? "" },
            { _ctx.Localizer["Excel.Confirmed"], b => b.IsConfirmed },
            { _ctx.Localizer["Excel.MedicalSheet"], b => b.IsMedicalSheet }
        };

        var title = _ctx.Localizer["Excel.BookingsSheet"];
        var pdfData = _exportServices.Pdf.ExportToPdf(bookings, title, columns);
        var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        return File(pdfData, "application/pdf", fileName);
    }

    private void StoreBookingFiltersToSession(BookingQueryParameters queryParams)
    {
        if (queryParams.ActivityId.HasValue)
            _ctx.Session.Set<int>(SessionKeyActivityId, queryParams.ActivityId.Value);
        if (!string.IsNullOrWhiteSpace(queryParams.SearchString))
            _ctx.Session.Set(SessionKeyBookingsSearchString, queryParams.SearchString, persistToCookie: false);
        if (queryParams.ChildId.HasValue)
            _ctx.Session.Set(SessionKeyBookingsChildId, queryParams.ChildId, persistToCookie: false);
        if (queryParams.IsConfirmed.HasValue)
            _ctx.Session.Set(SessionKeyBookingsIsConfirmed, queryParams.IsConfirmed, persistToCookie: false);
        if (!string.IsNullOrWhiteSpace(queryParams.SortBy))
            _ctx.Session.Set(SessionKeyBookingsSortBy, queryParams.SortBy, persistToCookie: false);
        if (!string.IsNullOrWhiteSpace(queryParams.SortOrder))
            _ctx.Session.Set(SessionKeyBookingsSortOrder, queryParams.SortOrder, persistToCookie: false);
        if (queryParams.PageNumber > 1)
            _ctx.Session.Set(SessionKeyBookingsPageNumber, queryParams.PageNumber.ToString(), persistToCookie: false);
    }

    private void ClearBookingFilters()
    {
        _ctx.Session.Clear(SessionKeyBookingsSearchString);
        _ctx.Session.Clear(SessionKeyBookingsChildId);
        _ctx.Session.Clear(SessionKeyBookingsIsConfirmed);
        _ctx.Session.Clear(SessionKeyBookingsSortBy);
        _ctx.Session.Clear(SessionKeyBookingsSortOrder);
        _ctx.Session.Clear(SessionKeyBookingsPageNumber);
    }

    private void LoadBookingFiltersFromSession(BookingQueryParameters queryParams)
    {
        queryParams.ActivityId = _ctx.Session.Get<int>(SessionKeyActivityId);
        queryParams.SearchString = _ctx.Session.Get(SessionKeyBookingsSearchString);
        queryParams.ChildId = _ctx.Session.Get<int>(SessionKeyBookingsChildId);
        queryParams.IsConfirmed = _ctx.Session.Get<bool>(SessionKeyBookingsIsConfirmed);
        queryParams.SortBy = _ctx.Session.Get(SessionKeyBookingsSortBy);
        queryParams.SortOrder = _ctx.Session.Get(SessionKeyBookingsSortOrder);
        var pageNumberStr = _ctx.Session.Get(SessionKeyBookingsPageNumber);
        if (!string.IsNullOrEmpty(pageNumberStr) && int.TryParse(pageNumberStr, out var pageNum))
            queryParams.PageNumber = pageNum;
    }
}
