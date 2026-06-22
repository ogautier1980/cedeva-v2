using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Contacts.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Contacts;

/// <summary>
/// Organisation-wide contact directory: app users (admins / coordinators), team members, parents,
/// and free-form "other contacts". Only the "other contacts" are editable here (the rest are managed
/// in their own modules).
/// </summary>
[Authorize]
public class ContactsController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ContactsController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index()
    {
        var orgId = _currentUserService.OrganisationId;

        var adminUsers = await _context.Users.Where(u => u.Role == Role.Admin)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync();
        var coordinatorUsers = await _context.Users
            .Where(u => u.Role == Role.Coordinator && u.OrganisationId == orgId)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync();
        var teamMembers = await _context.TeamMembers
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName).ToListAsync();
        var parents = await _context.Parents
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToListAsync();
        var others = await _context.Contacts
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync();

        var vm = new ContactsIndexViewModel
        {
            Admins = adminUsers.Select(u => new ContactRowViewModel
            {
                FullName = $"{u.LastName}, {u.FirstName}", Email = u.Email,
                Function = _localizer["Contacts.Admin"].Value
            }).ToList(),
            Coordinators = coordinatorUsers.Select(u => new ContactRowViewModel
            {
                FullName = $"{u.LastName}, {u.FirstName}", Email = u.Email,
                Function = _localizer["Contacts.Coordinator"].Value
            }).ToList(),
            TeamMembers = teamMembers.Select(t => new ContactRowViewModel
            {
                FullName = t.FullName, Email = t.Email,
                Function = _localizer[t.TeamRole == TeamRole.Coordinator ? "Contacts.Coordinator" : "Contacts.Animator"].Value
            }).ToList(),
            Parents = parents.Select(p => new ContactRowViewModel
            {
                FullName = p.FullName, Email = p.Email,
                Function = _localizer["Contacts.Parent"].Value
            }).ToList(),
            Others = others.Select(c => new ContactRowViewModel
            {
                FullName = c.FullName, Email = c.Email, Function = c.Function, ContactId = c.Id
            }).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create() => View(new ContactViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContactViewModel viewModel)
    {
        if (!ModelState.IsValid)
            return View(viewModel);

        var contact = new Contact
        {
            OrganisationId = _currentUserService.OrganisationId ?? 0,
            FirstName = viewModel.FirstName,
            LastName = viewModel.LastName,
            Email = viewModel.Email,
            PhoneNumber = viewModel.PhoneNumber,
            Function = viewModel.Function
        };
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();

        return this.RedirectToIndexWithSuccess(_localizer["Contacts.Created"].Value);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null)
            return NotFound();

        return View(new ContactViewModel
        {
            Id = contact.Id,
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            Email = contact.Email,
            PhoneNumber = contact.PhoneNumber,
            Function = contact.Function
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ContactViewModel viewModel)
    {
        if (!ModelState.IsValid)
            return View(viewModel);

        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == viewModel.Id);
        if (contact == null)
            return NotFound();

        contact.FirstName = viewModel.FirstName;
        contact.LastName = viewModel.LastName;
        contact.Email = viewModel.Email;
        contact.PhoneNumber = viewModel.PhoneNumber;
        contact.Function = viewModel.Function;
        await _context.SaveChangesAsync();

        return this.RedirectToIndexWithSuccess(_localizer["Contacts.Updated"].Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == id);
        if (contact != null)
        {
            _context.Contacts.Remove(contact);
            await _context.SaveChangesAsync();
            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["Contacts.Deleted"].Value;
        }
        return RedirectToAction(nameof(Index));
    }
}
