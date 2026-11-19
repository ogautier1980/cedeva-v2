using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Home.ViewModels;
using Cedeva.Website.Infrastructure;

namespace Cedeva.Website.Features.Home;

[Authorize]
public class HomeController : Controller
{
    private readonly CedevaDbContext _context;

    public HomeController(CedevaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Activities filtered by HasQueryFilter on OrganisationId
        var activities = await _context.Activities
            .Include(a => a.Bookings)
            .OrderByDescending(a => a.StartDate)
            .Take(5)
            .ToListAsync();

        var viewModel = new DashboardViewModel
        {
            RecentActivities = activities
                .Select(a => new ActivitySummary
                {
                    ActivityId = a.Id,
                    Name = a.Name,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    BookingsCount = a.Bookings?.Count ?? 0
                })
                .ToList()
        };

        return View(viewModel);
    }

    [AllowAnonymous]
    [HttpPost]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (UiLanguage.IsSupported(culture))
        {
            UiLanguage.WriteCookie(Response, culture);
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
