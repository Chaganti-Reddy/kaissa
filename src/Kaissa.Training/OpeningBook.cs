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
/// The opening book: every named ECO opening (CC0 Lichess dataset) plus a precomputed position index
/// so any position can be named and its book continuations listed. The index is built offline by
/// tools/OpeningImporter and shipped in openings.json, so at runtime this only deserializes lookup
/// tables - no replaying openings, no freeze. Pure logic, embedded data; unit-testable and portable.
/// Positions are keyed by placement+side+castling+ep (counters ignored) so transpositions unify.
/// </summary>
public sealed class OpeningBook
{
    private readonly List<OpeningEntry> _all;
    private readonly Dictionary<string, OpeningEntry> _named;    // position key -> opening naming it
    private readonly Dictionary<string, List<string>> _edges;    // position key -> book continuation moves (UCI)

    private OpeningBook(List<OpeningEntry> all, Dictionary<string, OpeningEntry> named, Dictionary<string, List<string>> edges)
    {
        _all = all; _named = named; _edges = edges;
    }

    public IReadOnlyList<OpeningEntry> All => _all;

    /// <summary>Kept for symmetry with other content; the index is always ready after Load.</summary>
    public bool IsIndexed => true;

    // The book is immutable once loaded, so parse it once per run and share it. Thread-safe, so a
    // launch-time preloader can warm it on a background thread and every page entry is then instant.
    private static OpeningBook? _default;
    private static readonly object _defaultLock = new();

    public static OpeningBook LoadDefault()
    {
        lock (_defaultLock)
        {
            if (_default != null) return _default;
            var assembly = typeof(OpeningBook).Assembly;
            var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("openings.json", StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException("Bundled openings.json is missing.");
            return _default = Load(stream);
        }
    }

    public static OpeningBook Load(Stream json)
    {
        var dto = JsonSerializer.Deserialize<ContentDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("openings.json could not be parsed.");

        var all = dto.Openings.Select(o => new OpeningEntry(o.Eco, o.Name, o.Uci, o.Fen)).ToList();
        var named = new Dictionary<string, OpeningEntry>(dto.Positions.Count);
        var edges = new Dictionary<string, List<string>>(dto.Positions.Count);
        foreach (var (key, pos) in dto.Positions)
        {
            if (!string.IsNullOrEmpty(pos.Name))
                named[key] = new OpeningEntry(pos.Eco ?? "", pos.Name!, Array.Empty<string>(), "");
            if (pos.Moves.Count > 0)
                edges[key] = pos.Moves;
        }
        return new OpeningBook(all, named, edges);
    }

    /// <summary>The opening that names this exact position, or null.</summary>
    public OpeningEntry? Name(string fen) => _named.TryGetValue(Key(fen), out var e) ? e : null;

    /// <summary>Book continuations from this position, each with the opening it leads to (if named).</summary>
    public IReadOnlyList<OpeningMove> Continuations(string fen)
    {
        if (!_edges.TryGetValue(Key(fen), out var moves))
            return Array.Empty<OpeningMove>();

        var game = ChessGame.FromFen(fen); // one position, computed on demand - cheap
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
        [JsonPropertyName("positions")] public Dictionary<string, PositionDto> Positions { get; init; } = new();
    }

    private sealed class OpeningDto
    {
        [JsonPropertyName("eco")] public string Eco { get; init; } = "";
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("uci")] public List<string> Uci { get; init; } = new();
        [JsonPropertyName("fen")] public string Fen { get; init; } = "";
    }

    private sealed class PositionDto
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("eco")] public string? Eco { get; init; }
        [JsonPropertyName("moves")] public List<string> Moves { get; init; } = new();
    }
}
