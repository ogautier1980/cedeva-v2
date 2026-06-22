using System.Globalization;
using Cedeva.Core.Entities;
using Cedeva.Core.Helpers;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Import;

/// <summary>
/// Imports activities (one per row), generating their days for the date range and copying the
/// organisation's email-template library into each. Org-scoped, de-duplicated by name.
/// </summary>
public class ActivityCsvImporter : ICsvEntityImporter
{
    private readonly CedevaDbContext _context;
    private readonly IEmailTemplateService _templateService;

    public ActivityCsvImporter(CedevaDbContext context, IEmailTemplateService templateService)
    {
        _context = context;
        _templateService = templateService;
    }

    public string Key => "activities";
    public string DisplayNameKey => "Import.Type.Activities";
    public bool AdminOnly => false;
    public string ColumnsTemplate => "Name;Description;StartDate;EndDate;PricePerDay;IsActive";

    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["name"] = new[] { "name", "nom", "titre" },
        ["description"] = new[] { "description" },
        ["startdate"] = new[] { "startdate", "datedebut", "debut" },
        ["enddate"] = new[] { "enddate", "datefin", "fin" },
        ["priceperday"] = new[] { "priceperday", "prixjour", "prixparjour", "tarif" },
        ["isactive"] = new[] { "isactive", "active", "actif" }
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

        var missing = data.MissingColumns(new[] { "name", "startdate", "enddate" });
        if (missing.Count > 0)
        {
            result.Errors.Add(CsvImportHelper.MissingColumnsMessage(missing));
            return result;
        }

        var existingNames = new HashSet<string>(
            await _context.Activities.Where(a => a.OrganisationId == organisationId)
                .Select(a => a.Name).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var createdActivityIds = new List<int>();

        foreach (var row in data.Rows)
        {
            result.RowsProcessed++;

            var name = row.Get("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(CsvImportHelper.RowError(row.LineNumber, "nom obligatoire."));
                continue;
            }

            if (!CsvImportHelper.TryParseDate(row.Get("startdate"), out var start) || !CsvImportHelper.TryParseDate(row.Get("enddate"), out var end))
            {
                result.Errors.Add(CsvImportHelper.RowError(row.LineNumber, "date de début ou de fin invalide."));
                continue;
            }
            if (end < start)
            {
                result.Errors.Add(CsvImportHelper.RowError(row.LineNumber, "la date de fin précède la date de début."));
                continue;
            }

            if (!existingNames.Add(name))
            {
                result.Skipped++; // an activity with this name already exists
                continue;
            }

            decimal? price = null;
            var priceRaw = row.Get("priceperday").Replace(',', '.');
            if (priceRaw.Length > 0 && decimal.TryParse(priceRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var p))
                price = p;

            var activity = new Activity
            {
                Name = name,
                Description = row.Get("description"),
                StartDate = start,
                EndDate = end,
                PricePerDay = price,
                IsActive = ParseBool(row.Get("isactive"), defaultValue: true),
                OrganisationId = organisationId
            };
            ActivityDayGenerator.GenerateDays(activity);

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync(ct);
            createdActivityIds.Add(activity.Id);
            result.Created++;
        }

        // Seed each new activity with the organisation's template library.
        foreach (var id in createdActivityIds)
            await _templateService.CopyOrganisationTemplatesToActivityAsync(organisationId, id);

        return result;
    }

    private static bool ParseBool(string raw, bool defaultValue)
    {
        var n = CsvImportHelper.Normalise(raw);
        if (n.Length == 0) return defaultValue;
        return n is "1" or "true" or "oui" or "yes" or "vrai" or "ja";
    }
}
