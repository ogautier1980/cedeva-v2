using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Home.ViewModels;

namespace Cedeva.Website.Features.Home;

[Authorize]
public class HomeController : Controller
{
    private readonly IRepository<Child> _childRepository;
    private readonly IRepository<Parent> _parentRepository;
    private readonly IRepository<TeamMember> _teamMemberRepository;
    private readonly CedevaDbContext _context;

    public HomeController(
        IRepository<Child> childRepository,
        IRepository<Parent> parentRepository,
        IRepository<TeamMember> teamMemberRepository,
        CedevaDbContext context)
    {
        _childRepository = childRepository;
        _parentRepository = parentRepository;
        _teamMemberRepository = teamMemberRepository;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var children = await _childRepository.GetAllAsync();
        var parents = await _parentRepository.GetAllAsync();
        var teamMembers = await _teamMemberRepository.GetAllAsync();

        // Load activities with bookings count using DbContext for proper Include
        var activities = await _context.Activities
            .Include(a => a.Bookings)
            .ToListAsync();

        // Load bookings with related entities (Child and Activity)
        var bookings = await _context.Bookings
            .Include(b => b.Child)
            .Include(b => b.Activity)
            .ToListAsync();

        var viewModel = new DashboardViewModel
        {
            TotalActivities = activities.Count,
            ActiveActivities = activities.Count(a => a.StartDate <= DateTime.Now && a.EndDate >= DateTime.Now),
            TotalBookings = bookings.Count,
            ConfirmedBookings = bookings.Count(b => b.IsConfirmed),
            TotalChildren = children.Count(),
            TotalParents = parents.Count(),
            TotalTeamMembers = teamMembers.Count(),
            RecentActivities = activities
                .OrderByDescending(a => a.StartDate)
                .Take(5)
                .Select(a => new ActivitySummary
                {
                    ActivityId = a.Id,
                    Name = a.Name,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    BookingsCount = a.Bookings?.Count ?? 0
                })
                .ToList(),
            RecentBookings = bookings
                .OrderByDescending(b => b.BookingDate)
                .Take(5)
                .Select(b => new BookingSummary
                {
                    BookingId = b.Id,
                    ChildName = $"{b.Child?.FirstName} {b.Child?.LastName}",
                    ActivityName = b.Activity?.Name ?? "N/A",
                    IsConfirmed = b.IsConfirmed,
                    CreatedAt = b.BookingDate
                })
                .ToList()
        };

        return View(viewModel);
    }

    [AllowAnonymous]
    [HttpPost]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        if (!string.IsNullOrEmpty(culture))
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax
                }
            );
        }

        return LocalRedirect(returnUrl ?? "/");
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
