using System.Globalization;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Helpers;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Import;

/// <summary>Imports parents (with address) and one child per row. Org-scoped.</summary>
public class ParentCsvImporter : ICsvEntityImporter
{
    private readonly CedevaDbContext _context;

    public ParentCsvImporter(CedevaDbContext context) => _context = context;

    public string Key => "parents";
    public string DisplayNameKey => "Import.Type.Parents";
    public bool AdminOnly => false;
    public string ColumnsTemplate =>
        "ParentFirstName;ParentLastName;Email;MobilePhone;ParentNRN;Street;PostalCode;City;ChildFirstName;ChildLastName;ChildBirthDate;ChildNRN";

    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["parentfirstname"] = new[] { "parentfirstname", "prenomparent", "parentprenom" },
        ["parentlastname"] = new[] { "parentlastname", "nomparent", "parentnom" },
        ["email"] = new[] { "email", "parentemail", "emailparent", "courriel" },
        ["mobilephone"] = new[] { "mobilephone", "gsm", "mobile", "gsmparent", "telephoneparent" },
        ["parentnrn"] = new[] { "parentnrn", "nrnparent", "registrenationalparent", "nissparent" },
        ["street"] = new[] { "street", "rue", "adresse" },
        ["postalcode"] = new[] { "postalcode", "codepostal", "cp" },
        ["city"] = new[] { "city", "ville", "commune" },
        ["childfirstname"] = new[] { "childfirstname", "prenomenfant", "enfantprenom" },
        ["childlastname"] = new[] { "childlastname", "nomenfant", "enfantnom" },
        ["childbirthdate"] = new[] { "childbirthdate", "datenaissance", "naissance", "birthdate" },
        ["childnrn"] = new[] { "childnrn", "nrnenfant", "registrenationalenfant" }
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
        {
            "parentfirstname", "parentlastname", "email", "mobilephone",
            "street", "postalcode", "city", "childfirstname", "childlastname", "childbirthdate"
        });
        if (missing.Count > 0)
        {
            result.Errors.Add($"Colonnes manquantes dans l'en-tête : {string.Join(", ", missing)}.");
            return result;
        }

        var parentsByEmail = await _context.Parents
            .Where(p => p.OrganisationId == organisationId)
            .ToDictionaryAsync(p => p.Email.Trim().ToLowerInvariant(), p => p, ct);
        var childNrns = new HashSet<string>(
            await _context.Children.Where(c => c.Parent.OrganisationId == organisationId && c.NationalRegisterNumber != "")
                .Select(c => c.NationalRegisterNumber).ToListAsync(ct));

        foreach (var row in data.Rows)
        {
            result.RowsProcessed++;

            var parentFirst = row.Get("parentfirstname");
            var parentLast = row.Get("parentlastname");
            var email = row.Get("email");
            var mobile = row.Get("mobilephone");
            var street = row.Get("street");
            var postalCode = row.Get("postalcode");
            var city = row.Get("city");
            var childFirst = row.Get("childfirstname");
            var childLast = row.Get("childlastname");
            var birthRaw = row.Get("childbirthdate");

            if (new[] { parentFirst, parentLast, email, mobile, street, postalCode, city, childFirst, childLast }
                .Any(string.IsNullOrWhiteSpace))
            {
                result.Errors.Add($"Ligne {row.LineNumber} : champ obligatoire manquant.");
                continue;
            }

            if (!DateTime.TryParseExact(birthRaw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            {
                result.Errors.Add($"Ligne {row.LineNumber} : date de naissance invalide « {birthRaw} » (attendu jj/mm/aaaa).");
                continue;
            }

            var parentNrn = NationalRegisterNumberHelper.StripFormatting(row.Get("parentnrn"));
            if (parentNrn.Length > 0 && !NationalRegisterNumberHelper.IsValid(parentNrn))
            {
                result.Errors.Add($"Ligne {row.LineNumber} : numéro de registre national du parent invalide.");
                continue;
            }

            var childNrn = NationalRegisterNumberHelper.StripFormatting(row.Get("childnrn"));
            if (childNrn.Length > 0 && !NationalRegisterNumberHelper.IsValid(childNrn))
            {
                result.Errors.Add($"Ligne {row.LineNumber} : numéro de registre national de l'enfant invalide.");
                continue;
            }

            var emailKey = email.ToLowerInvariant();
            if (!parentsByEmail.TryGetValue(emailKey, out var parent))
            {
                parent = new Parent
                {
                    FirstName = parentFirst,
                    LastName = parentLast,
                    Email = email,
                    MobilePhoneNumber = mobile,
                    NationalRegisterNumber = parentNrn,
                    OrganisationId = organisationId,
                    Address = new Address { Street = street, City = city, PostalCode = postalCode, Country = Country.Belgium }
                };
                _context.Parents.Add(parent);
                parentsByEmail[emailKey] = parent;
                result.Created++;
            }
            else
            {
                result.AddExtra("Import.ParentsReused");
            }

            if (childNrn.Length > 0 && !childNrns.Add(childNrn))
            {
                result.AddExtra("Import.ChildrenSkipped");
                continue;
            }

            _context.Children.Add(new Child
            {
                FirstName = childFirst,
                LastName = childLast,
                BirthDate = birthDate,
                NationalRegisterNumber = childNrn,
                Parent = parent
            });
            result.AddExtra("Import.ChildrenCreated");
        }

        if (result.Created > 0 || result.Extra.GetValueOrDefault("Import.ChildrenCreated") > 0)
            await _context.SaveChangesAsync(ct);

        return result;
    }
}
