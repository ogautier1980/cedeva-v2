using System.Globalization;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Helpers;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Import;

/// <summary>Imports team members (with address). Org-scoped, de-duplicated by email.</summary>
public class TeamMemberCsvImporter : ICsvEntityImporter
{
    private readonly CedevaDbContext _context;

    public TeamMemberCsvImporter(CedevaDbContext context) => _context = context;

    public string Key => "teammembers";
    public string DisplayNameKey => "Import.Type.TeamMembers";
    public bool AdminOnly => false;
    public string ColumnsTemplate =>
        "FirstName;LastName;Email;BirthDate;MobilePhone;NRN;Street;PostalCode;City;Role";

    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["firstname"] = new[] { "firstname", "prenom" },
        ["lastname"] = new[] { "lastname", "nom" },
        ["email"] = new[] { "email", "courriel" },
        ["birthdate"] = new[] { "birthdate", "datenaissance", "naissance" },
        ["mobilephone"] = new[] { "mobilephone", "gsm", "mobile", "telephone" },
        ["nrn"] = new[] { "nrn", "registrenational", "niss" },
        ["street"] = new[] { "street", "rue", "adresse" },
        ["postalcode"] = new[] { "postalcode", "codepostal", "cp" },
        ["city"] = new[] { "city", "ville", "commune" },
        ["role"] = new[] { "role", "teamrole", "fonction" }
    };

    private static readonly string[] DateFormats =
        { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy" };

    public async Task<CsvImportResult> ImportAsync(Stream csvStream, int organisationId, CancellationToken ct = default)
    {
        var result = new CsvImportResult();
        var data = await CsvImportHelper.ReadAsync(csvStream, Aliases, ct);
        if (data.IsEmpty)
        {
            result.Errors.Add("Le fichier est vide ou ne contient pas de données.");
            return result;
        }

        var missing = data.MissingColumns(new[]
        { "firstname", "lastname", "email", "birthdate", "mobilephone", "nrn", "street", "postalcode", "city" });
        if (missing.Count > 0)
        {
            result.Errors.Add($"Colonnes manquantes dans l'en-tête : {string.Join(", ", missing)}.");
            return result;
        }

        var existingEmails = new HashSet<string>(
            await _context.TeamMembers.Where(t => t.OrganisationId == organisationId)
                .Select(t => t.Email.ToLower()).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in data.Rows)
        {
            result.RowsProcessed++;

            var firstName = row.Get("firstname");
            var lastName = row.Get("lastname");
            var email = row.Get("email");
            var birthRaw = row.Get("birthdate");
            var mobile = row.Get("mobilephone");
            var street = row.Get("street");
            var postalCode = row.Get("postalcode");
            var city = row.Get("city");

            if (new[] { firstName, lastName, email, mobile, street, postalCode, city }.Any(string.IsNullOrWhiteSpace))
            {
                result.Errors.Add($"Ligne {row.LineNumber} : champ obligatoire manquant.");
                continue;
            }

            if (!DateTime.TryParseExact(birthRaw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            {
                result.Errors.Add($"Ligne {row.LineNumber} : date de naissance invalide « {birthRaw} ».");
                continue;
            }

            var nrn = NationalRegisterNumberHelper.StripFormatting(row.Get("nrn"));
            if (nrn.Length > 0 && !NationalRegisterNumberHelper.IsValid(nrn))
            {
                result.Errors.Add($"Ligne {row.LineNumber} : numéro de registre national invalide.");
                continue;
            }

            if (!existingEmails.Add(email))
            {
                result.Skipped++; // a team member with this email already exists
                continue;
            }

            _context.TeamMembers.Add(new TeamMember
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                BirthDate = birthDate,
                MobilePhoneNumber = mobile,
                NationalRegisterNumber = nrn,
                TeamRole = ParseRole(row.Get("role")),
                OrganisationId = organisationId,
                Address = new Address { Street = street, City = city, PostalCode = postalCode, Country = Country.Belgium }
            });
            result.Created++;
        }

        if (result.Created > 0)
            await _context.SaveChangesAsync(ct);

        return result;
    }

    private static TeamRole ParseRole(string raw)
    {
        var n = CsvImportHelper.Normalise(raw);
        return n is "coordinator" or "coordinateur" or "coordinatrice" ? TeamRole.Coordinator : TeamRole.Animator;
    }
}
