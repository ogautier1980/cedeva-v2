using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Infrastructure.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Import;

/// <summary>
/// Imports organisations (with address). Admin-only: creating tenants is cross-organisation, so this
/// importer ignores the org-scope parameter and is gated to admins by the controller.
/// </summary>
public class OrganisationCsvImporter : ICsvEntityImporter
{
    private readonly CedevaDbContext _context;

    public OrganisationCsvImporter(CedevaDbContext context) => _context = context;

    public string Key => "organisations";
    public string DisplayNameKey => "Import.Type.Organisations";
    public bool AdminOnly => true;
    public string ColumnsTemplate => "Name;Description;Street;PostalCode;City;Email";

    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["name"] = new[] { "name", "nom" },
        ["description"] = new[] { "description" },
        ["street"] = new[] { "street", "rue", "adresse" },
        ["postalcode"] = new[] { "postalcode", "codepostal", "cp" },
        ["city"] = new[] { "city", "ville", "commune" },
        ["email"] = new[] { "email", "courriel" }
    };

    public async Task<CsvImportResult> ImportAsync(Stream csvStream, int organisationId, CancellationToken ct = default)
    {
        var result = new CsvImportResult();
        var data = await CsvImportHelper.ReadAsync(csvStream, Aliases, ct);
        if (data.IsEmpty)
        {
            result.Errors.Add("Le fichier est vide ou ne contient pas de données.");
            return result;
        }

        var missing = data.MissingColumns(new[] { "name", "description", "street", "postalcode", "city" });
        if (missing.Count > 0)
        {
            result.Errors.Add($"Colonnes manquantes dans l'en-tête : {string.Join(", ", missing)}.");
            return result;
        }

        var existingNames = new HashSet<string>(
            await _context.Organisations.IgnoreQueryFilters().Select(o => o.Name).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);
        var created = new List<Organisation>();

        foreach (var row in data.Rows)
        {
            result.RowsProcessed++;

            var name = row.Get("name");
            var description = row.Get("description");
            var street = row.Get("street");
            var postalCode = row.Get("postalcode");
            var city = row.Get("city");

            if (new[] { name, description, street, postalCode, city }.Any(string.IsNullOrWhiteSpace))
            {
                result.Errors.Add($"Ligne {row.LineNumber} : champ obligatoire manquant.");
                continue;
            }

            if (!existingNames.Add(name))
            {
                result.Skipped++; // an organisation with this name already exists
                continue;
            }

            var email = row.Get("email");
            var organisation = new Organisation
            {
                Name = name,
                Description = description,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                Address = new Address { Street = street, City = city, PostalCode = postalCode, Country = Country.Belgium }
            };
            _context.Organisations.Add(organisation);
            created.Add(organisation);
            result.Created++;
        }

        if (result.Created > 0)
        {
            await _context.SaveChangesAsync(ct);
            // Give each new organisation the default email-template library.
            foreach (var org in created)
                await DefaultEmailTemplateLibrary.EnsureAsync(_context, org.Id, ct);
        }

        return result;
    }
}
