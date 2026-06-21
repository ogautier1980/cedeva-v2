using System.Text;

namespace Cedeva.Infrastructure.Services.Import;

/// <summary>
/// Shared CSV parsing for the entity importers: quote-aware splitting, ;/, delimiter detection,
/// and header-to-logical-column mapping via aliases (case/space/accent-insensitive on header names).
/// </summary>
public static class CsvImportHelper
{
    public static async Task<CsvData> ReadAsync(Stream stream, Dictionary<string, string[]> aliases, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(ct);
        var lines = content.Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n').Where(l => l.Trim().Length > 0).ToList();

        if (lines.Count < 2)
            return new CsvData(new Dictionary<string, int>(), new List<CsvDataRow>());

        var delimiter = lines[0].Contains(';') ? ';' : ',';
        var headers = SplitLine(lines[0], delimiter).Select(Normalise).ToList();

        var map = new Dictionary<string, int>();
        foreach (var (logical, names) in aliases)
            for (var i = 0; i < headers.Count; i++)
                if (names.Contains(headers[i])) { map[logical] = i; break; }

        var rows = new List<CsvDataRow>();
        for (var i = 1; i < lines.Count; i++)
            rows.Add(new CsvDataRow(i + 1, SplitLine(lines[i], delimiter), map));

        return new CsvData(map, rows);
    }

    internal static string Normalise(string header)
    {
        var sb = new StringBuilder();
        foreach (var c in header.Trim().ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Minimal quote-aware CSV line splitter (handles quoted fields + escaped quotes).</summary>
    internal static List<string> SplitLine(string line, char delimiter)
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

/// <summary>A parsed CSV: the logical-column map and the data rows.</summary>
public sealed class CsvData
{
    private readonly Dictionary<string, int> _map;
    public IReadOnlyList<CsvDataRow> Rows { get; }
    public bool IsEmpty => Rows.Count == 0;

    public CsvData(Dictionary<string, int> map, IReadOnlyList<CsvDataRow> rows)
    {
        _map = map;
        Rows = rows;
    }

    /// <summary>Returns the required logical columns that are absent from the header.</summary>
    public List<string> MissingColumns(IEnumerable<string> requiredLogicalColumns) =>
        requiredLogicalColumns.Where(c => !_map.ContainsKey(c)).ToList();
}

/// <summary>One CSV data row, addressable by logical column name.</summary>
public sealed class CsvDataRow
{
    private readonly List<string> _cells;
    private readonly Dictionary<string, int> _map;

    public int LineNumber { get; }

    public CsvDataRow(int lineNumber, List<string> cells, Dictionary<string, int> map)
    {
        LineNumber = lineNumber;
        _cells = cells;
        _map = map;
    }

    /// <summary>Trimmed value for a logical column, or "" if the column is absent/short.</summary>
    public string Get(string logicalColumn) =>
        _map.TryGetValue(logicalColumn, out var idx) && idx < _cells.Count ? _cells[idx].Trim() : string.Empty;
}
