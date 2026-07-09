using System.Text.Json;
using System.Text.Json.Serialization;
using Kaissa.Training;
using Kaissa.Training.Import;
using ZstdSharp;

// Imports scenarios from the Lichess puzzle database (CC0).
//
// Source resolution: try to download the database (3 attempts); if that fails (e.g. a locked-down
// network), fall back to a local copy. So it works on any machine, and a local file is optional.
//
//   dotnet run --project tools/ContentImporter -- [options]
// Options:
//   --url <url>           database URL (default: the official Lichess puzzle DB)
//   --local <path>        local fallback file, .zst or .csv (default: third_party/lichess/lichess_db_puzzle.csv.zst)
//   --no-download         skip the network and use the local file only
//   --out <path>          output JSON (default: generated-scenarios.json)
//   --min <rating>        minimum puzzle rating (default 600)
//   --max <rating>        maximum puzzle rating (default 1600)
//   --per-pattern <n>     cap scenarios per pattern (default 40)

const string defaultUrl = "https://database.lichess.org/lichess_db_puzzle.csv.zst";
const string defaultLocal = "third_party/lichess/lichess_db_puzzle.csv.zst";

string url = OptionValue("--url") ?? defaultUrl;
string local = OptionValue("--local") ?? defaultLocal;
bool noDownload = args.Contains("--no-download");
string output = OptionValue("--out") ?? "generated-scenarios.json";
int minRating = int.TryParse(OptionValue("--min"), out var lo) ? lo : 600;
int maxRating = int.TryParse(OptionValue("--max"), out var hi) ? hi : 1600;
int perPattern = int.TryParse(OptionValue("--per-pattern"), out var cap) ? cap : 40;

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

var source = noDownload ? null : await TryDownload(url);
source ??= TryLocal(local);

if (source is null)
{
    Console.Error.WriteLine(
        $"Could not download from {url} and no local file at {local}.\n" +
        $"Download it from https://database.lichess.org/#puzzles and place it there, or pass --local <path>.");
    return 1;
}

using var reader = source;

var kept = new List<Scenario>();
var perPatternCount = new Dictionary<PatternId, int>();
var skipReasons = new Dictionary<string, int>();
int scanned = 0;

string? line;
bool first = true;
while ((line = reader.ReadLine()) is not null)
{
    if (first)
    {
        first = false;
        if (line.StartsWith("PuzzleId", StringComparison.OrdinalIgnoreCase))
            continue; // header row
    }

    scanned++;

    if (!LichessPuzzleParser.TryParseRow(line, out var puzzle))
    {
        Bump(skipReasons, "unparseable row");
        continue;
    }

    if (puzzle.Rating < minRating || puzzle.Rating > maxRating)
        continue;

    var patternId = LichessPuzzleParser.MapPattern(puzzle.Themes);
    if (patternId is not { } id)
        continue;
    if (perPatternCount.GetValueOrDefault(id) >= perPattern)
        continue;

    if (!LichessPuzzleParser.TryBuildScenario(puzzle, out var scenario, out var reason))
    {
        Bump(skipReasons, reason);
        continue;
    }

    kept.Add(scenario);
    perPatternCount[scenario.Pattern] = perPatternCount.GetValueOrDefault(scenario.Pattern) + 1;

    if (perPatternCount.Count == LichessPuzzleParser.Catalog.Count &&
        perPatternCount.Values.All(c => c >= perPattern))
        break; // every pattern filled to the cap
}

WriteContent(output, kept);

Console.WriteLine($"\nScanned {scanned} rows, kept {kept.Count} scenarios -> {output}\n");
Console.WriteLine("Per pattern:");
foreach (var (pattern, count) in perPatternCount.OrderBy(kv => kv.Key.Value))
    Console.WriteLine($"  {pattern.Value,-28} {count}");
if (skipReasons.Count > 0)
{
    Console.WriteLine("\nSkipped:");
    foreach (var (reason, count) in skipReasons.OrderByDescending(kv => kv.Value))
        Console.WriteLine($"  {reason,-28} {count}");
}
return 0;

async Task<StreamReader?> TryDownload(string from)
{
    for (int attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            Console.WriteLine($"Downloading {from} (attempt {attempt}/3)...");
            var stream = await http.GetStreamAsync(from);
            return Wrap(stream, from.EndsWith(".zst", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  attempt {attempt} failed: {ex.Message}");
            if (attempt < 3)
                await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    Console.WriteLine("Download failed after 3 attempts; falling back to a local file.");
    return null;
}

StreamReader? TryLocal(string path)
{
    if (!File.Exists(path))
        return null;
    Console.WriteLine($"Reading local file {path}");
    return Wrap(File.OpenRead(path), path.EndsWith(".zst", StringComparison.OrdinalIgnoreCase));
}

static StreamReader Wrap(Stream stream, bool isZst) =>
    new(isZst ? new DecompressionStream(stream) : stream);

string? OptionValue(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static void Bump(Dictionary<string, int> counts, string key) =>
    counts[key] = counts.GetValueOrDefault(key) + 1;

static void WriteContent(string path, List<Scenario> scenarios)
{
    var patternDtos = scenarios.Select(s => s.Pattern).Distinct()
        .Select(p => LichessPuzzleParser.Catalog[p])
        .OrderBy(p => p.Id.Value)
        .Select(p => new PatternDto { Id = p.Id.Value, Name = p.Name, Description = p.Description })
        .ToList();

    var scenarioDtos = scenarios
        .OrderBy(s => s.Pattern.Value)
        .Select(s => new ScenarioDto
        {
            Id = s.Id,
            Pattern = s.Pattern.Value,
            Fen = s.Fen,
            Solutions = s.Solutions.ToList(),
            Prompt = s.Prompt,
            Rating = s.Rating,
            Line = s.SolverLine.ToList(),
            Themes = s.ThemeTags.ToList(),
            Setup = s.Setup ?? "",
        })
        .ToList();

    var content = new ContentDto { Patterns = patternDtos, Scenarios = scenarioDtos };
    // Compact: the file is generated content, not hand-edited, and a large set balloons if pretty-printed.
    File.WriteAllText(path, JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = false }));
}

file sealed class ContentDto
{
    [JsonPropertyName("patterns")] public List<PatternDto> Patterns { get; init; } = new();
    [JsonPropertyName("scenarios")] public List<ScenarioDto> Scenarios { get; init; } = new();
}

file sealed class PatternDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("description")] public string Description { get; init; } = "";
}

file sealed class ScenarioDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("pattern")] public string Pattern { get; init; } = "";
    [JsonPropertyName("fen")] public string Fen { get; init; } = "";
    [JsonPropertyName("solutions")] public List<string> Solutions { get; init; } = new();
    [JsonPropertyName("prompt")] public string Prompt { get; init; } = "";
    [JsonPropertyName("rating")] public int Rating { get; init; }
    [JsonPropertyName("line")] public List<string> Line { get; init; } = new();
    [JsonPropertyName("themes")] public List<string> Themes { get; init; } = new();
    [JsonPropertyName("setup")] public string Setup { get; init; } = "";
}
