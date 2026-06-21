using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Import;

/// <summary>Imports "other contacts". Org-scoped, de-duplicated by email (when present).</summary>
public class ContactCsvImporter : ICsvEntityImporter
{
    private readonly CedevaDbContext _context;

    public ContactCsvImporter(CedevaDbContext context) => _context = context;

    public string Key => "contacts";
    public string DisplayNameKey => "Import.Type.Contacts";
    public bool AdminOnly => false;
    public string ColumnsTemplate => "FirstName;LastName;Email;Phone;Function";

    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["firstname"] = new[] { "firstname", "prenom" },
        ["lastname"] = new[] { "lastname", "nom" },
        ["email"] = new[] { "email", "courriel" },
        ["phone"] = new[] { "phone", "telephone", "gsm", "mobile" },
        ["function"] = new[] { "function", "fonction", "role" }
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

        var missing = data.MissingColumns(new[] { "firstname", "lastname" });
        if (missing.Count > 0)
        {
            result.Errors.Add($"Colonnes manquantes dans l'en-tête : {string.Join(", ", missing)}.");
            return result;
        }

        var existingEmails = new HashSet<string>(
            await _context.Contacts.Where(c => c.OrganisationId == organisationId && c.Email != null && c.Email != "")
                .Select(c => c.Email!.ToLower()).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in data.Rows)
        {
            result.RowsProcessed++;

            var firstName = row.Get("firstname");
            var lastName = row.Get("lastname");
            var email = row.Get("email");

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                result.Errors.Add($"Ligne {row.LineNumber} : nom et prénom obligatoires.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(email) && !existingEmails.Add(email))
            {
                result.Skipped++;
                continue;
            }

            _context.Contacts.Add(new Contact
            {
                OrganisationId = organisationId,
                FirstName = firstName,
                LastName = lastName,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                PhoneNumber = EmptyToNull(row.Get("phone")),
                Function = EmptyToNull(row.Get("function"))
            });
            result.Created++;
        }

        if (result.Created > 0)
            await _context.SaveChangesAsync(ct);

        return result;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
