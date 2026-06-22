using Cedeva.Core.Entities;
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
/// CRUD for saved, reusable contact groups (named lists of recipients built from the organisation's
/// contacts). Groups are selectable as a recipient in the email composer.
/// </summary>
[Authorize]
public class ContactGroupsController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IContactDirectoryService _contactDirectory;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ContactGroupsController(
        CedevaDbContext context,
        ICurrentUserService currentUserService,
        IContactDirectoryService contactDirectory,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _contactDirectory = contactDirectory;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index()
    {
        var groups = await _context.ContactGroups
            .Include(g => g.Members)
            .OrderBy(g => g.Name)
            .ToListAsync();

        var vm = new ContactGroupListViewModel
        {
            Groups = groups.Select(g => new ContactGroupRowViewModel
            {
                Id = g.Id,
                Name = g.Name,
                MemberCount = g.Members.Count,
                MemberNames = g.Members.OrderBy(m => m.DisplayName)
                    .Select(m => m.DisplayName ?? m.Email).ToList()
            }).ToList()
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new ContactGroupFormViewModel { AvailableContacts = await GetAvailableContactsAsync() };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContactGroupFormViewModel viewModel)
    {
        var available = await GetAvailableContactsAsync();
        var members = ResolveMembers(viewModel.SelectedEmails, available);

        if (members.Count == 0)
            ModelState.AddModelError(nameof(viewModel.SelectedEmails), _localizer["ContactGroup.NoMembersSelected"].Value);

        if (!ModelState.IsValid)
        {
            viewModel.AvailableContacts = available;
            return View(viewModel);
        }

        var group = new ContactGroup
        {
            OrganisationId = _currentUserService.OrganisationId ?? 0,
            Name = viewModel.Name,
            Members = members
        };
        _context.ContactGroups.Add(group);
        await _context.SaveChangesAsync();

        return this.RedirectToIndexWithSuccess(_localizer["ContactGroup.Created"].Value);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await _context.ContactGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id);
        if (group == null)
            return NotFound();

        return View(new ContactGroupFormViewModel
        {
            Id = group.Id,
            Name = group.Name,
            SelectedEmails = group.Members.Select(m => m.Email).ToList(),
            AvailableContacts = await GetAvailableContactsAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ContactGroupFormViewModel viewModel)
    {
        var group = await _context.ContactGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == viewModel.Id);
        if (group == null)
            return NotFound();

        var available = await GetAvailableContactsAsync();
        var members = ResolveMembers(viewModel.SelectedEmails, available);

        if (members.Count == 0)
            ModelState.AddModelError(nameof(viewModel.SelectedEmails), _localizer["ContactGroup.NoMembersSelected"].Value);

        if (!ModelState.IsValid)
        {
            viewModel.AvailableContacts = available;
            return View(viewModel);
        }

        group.Name = viewModel.Name;
        _context.ContactGroupMembers.RemoveRange(group.Members);
        group.Members = members;
        await _context.SaveChangesAsync();

        return this.RedirectToIndexWithSuccess(_localizer["ContactGroup.Updated"].Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await _context.ContactGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group != null)
        {
            _context.ContactGroups.Remove(group);
            await _context.SaveChangesAsync();
            TempData[ControllerExtensions.SuccessMessageKey] = _localizer["ContactGroup.Deleted"].Value;
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<ContactPickItem>> GetAvailableContactsAsync()
    {
        var contacts = await _contactDirectory.GetSelectableContactsAsync(_currentUserService.OrganisationId ?? 0);
        return contacts.Select(c => new ContactPickItem { Email = c.Email, Display = c.Display, Source = c.Source }).ToList();
    }

    /// <summary>Keeps only the selected emails that are real contacts of the organisation.</summary>
    private static List<ContactGroupMember> ResolveMembers(IEnumerable<string>? selectedEmails, List<ContactPickItem> available)
    {
        var byEmail = available.ToDictionary(c => c.Email, StringComparer.OrdinalIgnoreCase);
        return (selectedEmails ?? Enumerable.Empty<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(byEmail.ContainsKey)
            .Select(e => new ContactGroupMember { Email = e, DisplayName = byEmail[e].Display })
            .ToList();
    }
}
