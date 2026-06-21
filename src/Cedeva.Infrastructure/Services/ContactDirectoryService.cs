using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services;

/// <inheritdoc cref="IContactDirectoryService"/>
public class ContactDirectoryService : IContactDirectoryService
{
    private readonly CedevaDbContext _context;

    public ContactDirectoryService(CedevaDbContext context) => _context = context;

    public async Task<List<SelectableContact>> GetSelectableContactsAsync(int organisationId, CancellationToken ct = default)
    {
        var parents = await _context.Parents
            .Where(p => p.OrganisationId == organisationId && p.Email != "")
            .Select(p => new SelectableContact(p.Email, p.LastName + ", " + p.FirstName, ContactSource.Parent))
            .ToListAsync(ct);
        var teamMembers = await _context.TeamMembers
            .Where(t => t.OrganisationId == organisationId && t.Email != "")
            .Select(t => new SelectableContact(t.Email, t.LastName + ", " + t.FirstName, ContactSource.TeamMember))
            .ToListAsync(ct);
        var others = await _context.Contacts
            .Where(c => c.OrganisationId == organisationId && c.Email != null && c.Email != "")
            .Select(c => new SelectableContact(c.Email!, c.LastName + ", " + c.FirstName, ContactSource.Other))
            .ToListAsync(ct);

        return parents.Concat(teamMembers).Concat(others)
            .GroupBy(c => c.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(c => c.Source).ThenBy(c => c.Display)
            .ToList();
    }
}
