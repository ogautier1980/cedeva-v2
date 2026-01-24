using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Website.Features.Home.ViewModels;

namespace Cedeva.Website.Features.Home;

[Authorize]
public class HomeController : Controller
{
    private readonly IRepository<Activity> _activityRepository;
    private readonly IRepository<Booking> _bookingRepository;
    private readonly IRepository<Child> _childRepository;
    private readonly IRepository<Parent> _parentRepository;
    private readonly IRepository<TeamMember> _teamMemberRepository;

    public HomeController(
        IRepository<Activity> activityRepository,
        IRepository<Booking> bookingRepository,
        IRepository<Child> childRepository,
        IRepository<Parent> parentRepository,
        IRepository<TeamMember> teamMemberRepository)
    {
        _activityRepository = activityRepository;
        _bookingRepository = bookingRepository;
        _childRepository = childRepository;
        _parentRepository = parentRepository;
        _teamMemberRepository = teamMemberRepository;
    }

    public async Task<IActionResult> Index()
    {
        var activities = await _activityRepository.GetAllAsync();
        var bookings = await _bookingRepository.GetAllAsync();
        var children = await _childRepository.GetAllAsync();
        var parents = await _parentRepository.GetAllAsync();
        var teamMembers = await _teamMemberRepository.GetAllAsync();

        var viewModel = new DashboardViewModel
        {
            TotalActivities = activities.Count(),
            ActiveActivities = activities.Count(a => a.StartDate <= DateTime.Now && a.EndDate >= DateTime.Now),
            TotalBookings = bookings.Count(),
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
                    BookingsCount = a.Bookings.Count
                })
                .ToList(),
            RecentBookings = bookings
                .OrderByDescending(b => b.BookingDate)
                .Take(5)
                .Select(b => new BookingSummary
                {
                    BookingId = b.Id,
                    ChildName = $"{b.Child.FirstName} {b.Child.LastName}",
                    ActivityName = b.Activity.Name,
                    IsConfirmed = b.IsConfirmed,
                    CreatedAt = b.BookingDate
                })
                .ToList()
        };

        return View(viewModel);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
