using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>One named opening line: ECO code, name, the moves in UCI, and the FEN reached.</summary>
public sealed record OpeningEntry(string Eco, string Name, IReadOnlyList<string> Uci, string Fen)
{
    /// <summary>Grouping for the browse list, by the opening's first move.</summary>
    public string Group => Uci.Count == 0 ? "Other"
        : Uci[0] == "e2e4" ? "1. e4"
        : Uci[0] == "d2d4" ? "1. d4"
        : "Other";
}

/// <summary>A book continuation from a position: the move, its SAN, and where it leads (if named).</summary>
public sealed record OpeningMove(string Uci, string San, string? Name, string? Eco);

/// <summary>
/// The opening book: every named ECO opening (from the CC0 Lichess dataset) plus a position tree so
/// any position can be named and its book continuations listed. Pure logic, embedded data, so it is
/// unit-testable and portable. Positions are keyed by placement+side+castling+ep (counters ignored),
/// so transpositions unify.
/// </summary>
public sealed class OpeningBook
{
    private readonly List<OpeningEntry> _all;
    private readonly Dictionary<string, OpeningEntry> _named = new();       // position key -> named opening ending there
    private readonly Dictionary<string, List<string>> _edges = new();        // position key -> book continuation moves (UCI)

    private OpeningBook(List<OpeningEntry> all) => _all = all;

    public IReadOnlyList<OpeningEntry> All => _all;

    /// <summary>True once the position tree is built and Name/Continuations are usable.</summary>
    public bool IsIndexed { get; private set; }

    /// <summary>Parses the bundled book and builds its tree eagerly (for core/tests).</summary>
    public static OpeningBook LoadDefault() => Load(OpenDefault());

    /// <summary>Parses the bundled book WITHOUT building the tree, so a caller can build it
    /// incrementally (see <see cref="BuildTreeChunked"/>) and keep the UI responsive.</summary>
    public static OpeningBook LoadDeferredDefault() => ParseOnly(OpenDefault());

    public static OpeningBook Load(Stream json)
    {
        var book = ParseOnly(json);
        book.BuildTree();
        return book;
    }

    private static OpeningBook ParseOnly(Stream json)
    {
        var dto = JsonSerializer.Deserialize<ContentDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("openings.json could not be parsed.");
        var all = dto.Openings.Select(o => new OpeningEntry(o.Eco, o.Name, o.Uci, o.Fen)).ToList();
        return new OpeningBook(all);
    }

    private static Stream OpenDefault()
    {
        var assembly = typeof(OpeningBook).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("openings.json", StringComparison.Ordinal));
        return assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Bundled openings.json is missing.");
    }

    /// <summary>Builds the position tree a chunk of entries at a time, yielding between chunks so a
    /// coroutine can spread the work across frames. Iterate to completion before calling Name/Continuations.</summary>
    public IEnumerator<bool> BuildTreeChunked(int perStep)
    {
        int i = 0;
        foreach (var entry in _all)
        {
            ProcessEntry(entry);
            if (++i % perStep == 0) yield return true;
        }
        IsIndexed = true;
        yield return false;
    }

    /// <summary>The opening that names this exact position, or null.</summary>
    public OpeningEntry? Name(string fen) => _named.TryGetValue(Key(fen), out var e) ? e : null;

    /// <summary>Book continuations from this position, each with the opening it leads to (if named).</summary>
    public IReadOnlyList<OpeningMove> Continuations(string fen)
    {
        if (!_edges.TryGetValue(Key(fen), out var moves))
            return Array.Empty<OpeningMove>();

        var game = ChessGame.FromFen(fen);
        var result = new List<OpeningMove>(moves.Count);
        foreach (var uci in moves)
        {
            var san = game.SanForUci(uci) ?? uci;
            string? name = null, eco = null;
            var after = Apply(fen, uci);
            if (after != null && _named.TryGetValue(Key(after), out var e)) { name = e.Name; eco = e.Eco; }
            result.Add(new OpeningMove(uci, san, name, eco));
        }
        return result;
    }

    /// <summary>Openings grouped by first move (1. e4 / 1. d4 / Other), each list name-sorted.</summary>
    public IReadOnlyList<(string Group, IReadOnlyList<OpeningEntry> Entries)> Grouped()
    {
        var order = new[] { "1. e4", "1. d4", "Other" };
        return order
            .Select(g => (g, (IReadOnlyList<OpeningEntry>)_all.Where(e => e.Group == g)
                .OrderBy(e => e.Name, StringComparer.Ordinal).ToList()))
            .ToList();
    }

    private void BuildTree()
    {
        foreach (var entry in _all) ProcessEntry(entry);
        IsIndexed = true;
    }

    private void ProcessEntry(OpeningEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.Fen))
        {
            var termKey = Key(entry.Fen);
            // Prefer the longest line naming a position (most specific), else keep the first.
            if (!_named.TryGetValue(termKey, out var cur) || entry.Uci.Count > cur.Uci.Count)
                _named[termKey] = entry;
        }

        // Replay to record each book edge (position -> next move).
        var game = ChessGame.Start();
        foreach (var uci in entry.Uci)
        {
            var key = Key(game.Fen);
            if (!_edges.TryGetValue(key, out var list)) _edges[key] = list = new List<string>();
            if (!list.Contains(uci)) list.Add(uci);
            if (!game.TryMakeMove(uci)) break;
        }
    }

    private static string? Apply(string fen, string uci)
    {
        try { var g = ChessGame.FromFen(fen); return g.TryMakeMove(uci) ? g.Fen : null; }
        catch { return null; }
    }

    // Position identity: placement + side + castling + en-passant (ignore the move counters).
    private static string Key(string fen)
    {
        var p = fen.Split(' ');
        return p.Length >= 4 ? $"{p[0]} {p[1]} {p[2]} {p[3]}" : fen;
    }

    private sealed class ContentDto
    {
        [JsonPropertyName("openings")] public List<OpeningDto> Openings { get; init; } = new();
    }

    private sealed class OpeningDto
    {
        [JsonPropertyName("eco")] public string Eco { get; init; } = "";
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("uci")] public List<string> Uci { get; init; } = new();
        [JsonPropertyName("fen")] public string Fen { get; init; } = "";
    }
}
