using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;

namespace NavigationPlan.Services;

public static class AirportLookupService
{
    public record AirportInfo(string Ident, string Name, double Lat, double Lon);

    private const string CsvUrl   = "https://davidmegginson.github.io/ourairports-data/airports.csv";
    private static readonly string CacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NavigationPlan", "airports.csv");

    private static Dictionary<string, AirportInfo>? _index;
    private static Task? _loadTask;

    public static bool IsLoaded => _index != null;

    // Call once on startup — fire-and-forget is fine
    public static Task EnsureLoadedAsync()
    {
        _loadTask ??= LoadCoreAsync();
        return _loadTask;
    }

    public static AirportInfo? Lookup(string icao) =>
        _index != null && _index.TryGetValue(icao.Trim(), out var info) ? info : null;

    private static async Task LoadCoreAsync()
    {
        if (!File.Exists(CacheFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var data = await http.GetStringAsync(CsvUrl);
            await File.WriteAllTextAsync(CacheFile, data, Encoding.UTF8);
        }

        var index = new Dictionary<string, AirportInfo>(StringComparer.OrdinalIgnoreCase);
        bool first = true;
        foreach (var line in await File.ReadAllLinesAsync(CacheFile, Encoding.UTF8))
        {
            if (first) { first = false; continue; } // skip header
            var f = ParseCsvLine(line);
            if (f.Length < 6) continue;
            var ident = f[1];
            var name  = f[3];
            if (string.IsNullOrEmpty(ident)) continue;
            if (!double.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
            if (!double.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;
            index[ident] = new AirportInfo(ident, name, lat, lon);
        }
        _index = index;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb     = new StringBuilder();
        bool inQ   = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQ)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') inQ = false;
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQ = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return [.. fields];
    }
}
