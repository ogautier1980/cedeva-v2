using Cedeva.Core.Entities;
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

    public async Task<CsvImportResult> ImportAsync(Stream csvStream, int organisationId, CancellationToken ct = default)
    {
        var result = new CsvImportResult();
        var data = await CsvImportHelper.ReadAsync(csvStream, Aliases, ct);
        if (data.IsEmpty)
        {
            result.Errors.Add(CsvImportHelper.EmptyFileMessage);
            return result;
        }

        var missing = data.MissingColumns(new[]
        {
            "parentfirstname", "parentlastname", "email", "mobilephone",
            "street", "postalcode", "city", "childfirstname", "childlastname", "childbirthdate"
        });
        if (missing.Count > 0)
        {
            result.Errors.Add(CsvImportHelper.MissingColumnsMessage(missing));
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

            if (new[] { parentFirst, parentLast, email, mobile, street, postalCode, city, childFirst, childLast }
                .Any(string.IsNullOrWhiteSpace))
            {
                result.Errors.Add(CsvImportHelper.RowError(row.LineNumber, "champ obligatoire manquant."));
                continue;
            }

            if (!CsvImportHelper.TryParseDate(row.Get("childbirthdate"), out var birthDate))
            {
                result.Errors.Add(CsvImportHelper.RowError(row.LineNumber, $"date de naissance invalide « {row.Get("childbirthdate")} » (attendu jj/mm/aaaa)."));
                continue;
            }

            if (!CsvImportHelper.TryGetValidNrn(row, "parentnrn", out var parentNrn))
            {
                result.Errors.Add(CsvImportHelper.RowError(row.LineNumber, "numéro de registre national du parent invalide."));
                continue;
            }
            if (!CsvImportHelper.TryGetValidNrn(row, "childnrn", out var childNrn))
            {
                result.Errors.Add(CsvImportHelper.RowError(row.LineNumber, "numéro de registre national de l'enfant invalide."));
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
                    Address = CsvImportHelper.BelgiumAddress(street, postalCode, city)
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
