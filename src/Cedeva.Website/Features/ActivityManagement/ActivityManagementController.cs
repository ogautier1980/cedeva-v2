using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.ActivityManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Website.Features.ActivityManagement;

[Authorize]
public class ActivityManagementController : Controller
{
    private const string SessionActivityId = "Activity_Id";

    private readonly CedevaDbContext _context;
    private readonly ILogger<ActivityManagementController> _logger;

    public ActivityManagementController(
        CedevaDbContext context,
        ILogger<ActivityManagementController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id)
    {
        if (id is null)
        {
            var idStr = HttpContext.Session.GetString(SessionActivityId);
            if (int.TryParse(idStr, out var parsed))
            {
                id = parsed;
            }
        }

        if (id is null)
            return NotFound();

        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.Groups)
            .Include(a => a.Bookings)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound();

        var viewModel = new IndexViewModel
        {
            Activity = activity
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Index")]
    public IActionResult IndexPost(int id)
    {
        HttpContext.Session.SetString(SessionActivityId, id.ToString());

        return RedirectToAction(nameof(Index));
    }
}
