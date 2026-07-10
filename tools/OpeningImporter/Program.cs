using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kaissa.Chess.Rules;

// Converts the Lichess chess-openings TSVs (CC0) into a compact openings.json the app embeds.
// Each row is: eco <tab> name <tab> pgn (SAN, with move numbers). We replay the SAN through the
// rules engine to get canonical UCI, and record the FEN reached so the app can name any position.
//
//   dotnet run --project tools/OpeningImporter -- [--in <dir>] [--out <path>]
//     --in   directory of a.tsv..e.tsv (default third_party/openings)
//     --out  output JSON (default src/Kaissa.Training/Content/openings.json)

string inDir = OptionValue("--in") ?? "third_party/openings";
string output = OptionValue("--out") ?? "src/Kaissa.Training/Content/openings.json";

var entries = new List<OpeningDto>();
int scanned = 0, skipped = 0;

foreach (var file in new[] { "a", "b", "c", "d", "e" })
{
    var path = Path.Combine(inDir, file + ".tsv");
    if (!File.Exists(path)) { Console.Error.WriteLine($"missing {path}"); continue; }

    bool header = true;
    foreach (var line in File.ReadLines(path))
    {
        if (header) { header = false; continue; }
        if (string.IsNullOrWhiteSpace(line)) continue;
        var f = line.Split('\t');
        if (f.Length < 3) continue;
        scanned++;

        var uci = SanLineToUci(f[2], out var fen);
        if (uci is null) { skipped++; continue; }

        entries.Add(new OpeningDto
        {
            Eco = f[0].Trim(),
            Name = f[1].Trim(),
            Uci = uci,
            Fen = fen,
        });
    }
}

// Sort by move count then name so prefix openings come before their extensions (helps longest-match).
entries.Sort((a, b) => a.Uci.Count != b.Uci.Count ? a.Uci.Count - b.Uci.Count
    : string.CompareOrdinal(a.Name, b.Name));

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(new ContentDto { Openings = entries },
    new JsonSerializerOptions { WriteIndented = false }));

Console.WriteLine($"Scanned {scanned}, kept {entries.Count}, skipped {skipped} -> {output}");
return 0;

static List<string>? SanLineToUci(string pgn, out string fen)
{
    fen = ChessGame.StartFen;
    var game = ChessGame.Start();
    var uci = new List<string>();
    foreach (var raw in pgn.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    {
        var tok = raw.Trim();
        if (tok.Length == 0) continue;
        if (char.IsDigit(tok[0]) && tok.Contains('.'))
        {
            // "1." or "1.e4" — strip the leading move number.
            int dot = tok.IndexOf('.');
            tok = tok[(dot + 1)..];
            if (tok.Length == 0) continue;
        }
        var san = tok.TrimEnd('+', '#', '!', '?');
        var resolved = game.ResolveToUci(san) ?? game.ResolveToUci(tok);
        if (resolved is null || !game.TryMakeMove(resolved))
            return null; // unparseable line — skip it
        uci.Add(resolved);
    }
    if (uci.Count == 0) return null;
    fen = game.Fen;
    return uci;
}

static string? OptionValue(string name)
{
    var args = Environment.GetCommandLineArgs();
    for (int i = 1; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
}

file sealed class ContentDto
{
    [JsonPropertyName("openings")] public List<OpeningDto> Openings { get; init; } = new();
}

file sealed class OpeningDto
{
    [JsonPropertyName("eco")] public string Eco { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("uci")] public List<string> Uci { get; init; } = new();
    [JsonPropertyName("fen")] public string Fen { get; init; } = "";
}
