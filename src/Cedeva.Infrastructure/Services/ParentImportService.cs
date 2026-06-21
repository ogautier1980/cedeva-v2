using System.Globalization;
using System.Text;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Helpers;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services;

/// <inheritdoc cref="IParentImportService"/>
public class ParentImportService : IParentImportService
{
    private readonly CedevaDbContext _context;

    public ParentImportService(CedevaDbContext context) => _context = context;

    // Logical columns mapped from header names (normalised: lowercased, alphanumeric only).
    private static readonly Dictionary<string, string[]> ColumnAliases = new()
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

    public async Task<ParentImportResult> ImportAsync(Stream csvStream, int organisationId, CancellationToken ct = default)
    {
        var result = new ParentImportResult();

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(ct);
        var lines = content.Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n').Where(l => l.Trim().Length > 0).ToList();

        if (lines.Count < 2)
        {
            result.Errors.Add("Le fichier est vide ou ne contient pas de données.");
            return result;
        }

        var delimiter = lines[0].Contains(';') ? ';' : ',';
        var headerCells = SplitCsvLine(lines[0], delimiter).Select(Normalise).ToList();
        var map = BuildColumnMap(headerCells);

        var missing = new[] { "parentfirstname", "parentlastname", "email", "mobilephone", "street", "postalcode", "city", "childfirstname", "childlastname", "childbirthdate" }
            .Where(c => !map.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            result.Errors.Add($"Colonnes manquantes dans l'en-tête : {string.Join(", ", missing)}.");
            return result;
        }

        // Existing data for de-duplication (loaded once, tracked so children attach to reused parents).
        var parentsByEmail = await _context.Parents
            .Where(p => p.OrganisationId == organisationId)
            .ToDictionaryAsync(p => p.Email.Trim().ToLowerInvariant(), p => p, ct);
        var childNrns = new HashSet<string>(
            await _context.Children.Where(c => c.Parent.OrganisationId == organisationId && c.NationalRegisterNumber != "")
                .Select(c => c.NationalRegisterNumber).ToListAsync(ct));

        for (var i = 1; i < lines.Count; i++)
        {
            result.RowsProcessed++;
            var rowNumber = i + 1; // 1-based incl. header
            var cells = SplitCsvLine(lines[i], delimiter);

            string Get(string col) => map.TryGetValue(col, out var idx) && idx < cells.Count ? cells[idx].Trim() : string.Empty;

            var parentFirst = Get("parentfirstname");
            var parentLast = Get("parentlastname");
            var email = Get("email");
            var mobile = Get("mobilephone");
            var street = Get("street");
            var postalCode = Get("postalcode");
            var city = Get("city");
            var childFirst = Get("childfirstname");
            var childLast = Get("childlastname");
            var birthRaw = Get("childbirthdate");

            if (string.IsNullOrWhiteSpace(parentFirst) || string.IsNullOrWhiteSpace(parentLast) ||
                string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(mobile) ||
                string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(postalCode) || string.IsNullOrWhiteSpace(city) ||
                string.IsNullOrWhiteSpace(childFirst) || string.IsNullOrWhiteSpace(childLast))
            {
                result.Errors.Add($"Ligne {rowNumber} : champ obligatoire manquant.");
                continue;
            }

            if (!DateTime.TryParseExact(birthRaw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            {
                result.Errors.Add($"Ligne {rowNumber} : date de naissance invalide « {birthRaw} » (attendu jj/mm/aaaa).");
                continue;
            }

            var parentNrn = NationalRegisterNumberHelper.StripFormatting(Get("parentnrn"));
            if (parentNrn.Length > 0 && !NationalRegisterNumberHelper.IsValid(parentNrn))
            {
                result.Errors.Add($"Ligne {rowNumber} : numéro de registre national du parent invalide.");
                continue;
            }

            var childNrn = NationalRegisterNumberHelper.StripFormatting(Get("childnrn"));
            if (childNrn.Length > 0 && !NationalRegisterNumberHelper.IsValid(childNrn))
            {
                result.Errors.Add($"Ligne {rowNumber} : numéro de registre national de l'enfant invalide.");
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
                result.ParentsCreated++;
            }
            else
            {
                result.ParentsReused++;
            }

            if (childNrn.Length > 0 && !childNrns.Add(childNrn))
            {
                result.ChildrenSkipped++; // a child with this NRN already exists
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
            result.ChildrenCreated++;
        }

        if (result.ParentsCreated > 0 || result.ChildrenCreated > 0)
            await _context.SaveChangesAsync(ct);

        return result;
    }

    private static Dictionary<string, int> BuildColumnMap(List<string> normalisedHeaders)
    {
        var map = new Dictionary<string, int>();
        foreach (var (logical, aliases) in ColumnAliases)
        {
            for (var i = 0; i < normalisedHeaders.Count; i++)
            {
                if (aliases.Contains(normalisedHeaders[i]))
                {
                    map[logical] = i;
                    break;
                }
            }
        }
        return map;
    }

    private static string Normalise(string header)
    {
        var sb = new StringBuilder();
        foreach (var c in header.Trim().ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Minimal quote-aware CSV line splitter (handles quoted fields + escaped quotes).</summary>
    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return result;
    }
}
