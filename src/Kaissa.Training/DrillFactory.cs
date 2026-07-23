using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>The named training drills, generated from the shared puzzle content and the player's weak spots.</summary>
public enum DrillKind
{
    TimeTrainer,             // best move across varied positions, under a clock (client times it)
    Intuition,               // trust the first instinct; varied positions, quick answers
    Defender,                // you are down material - find the resource that holds
    AdvantageCapitalization, // you are winning - convert it, don't let it slip
    BlunderPreventer,        // pick the stronger of two candidate moves
    CheckmatePatterns,       // timed mate flashcards
    OpeningImprover,         // early-position mistakes to clean up
}

/// <summary>A generated drill: a titled, described, ordered set of positions to work through.</summary>
public sealed record Drill(DrillKind Kind, string Title, string Description, IReadOnlyList<Scenario> Scenarios)
{
    public int Count => Scenarios.Count;
}

/// <summary>
/// A "pick the stronger move" problem (Blunder Preventer): one clearly good move (the puzzle
/// solution) shown against one plausible alternative, in a fixed display order. The alternative is a
/// legal-but-unremarkable move, not a claim about the engine's second choice - the point is to train
/// the choice itself.
/// </summary>
public sealed record TwoChoiceProblem(
    string Fen,
    string Prompt,
    string BetterUci,
    string WorseUci,
    IReadOnlyList<string> Options);

/// <summary>
/// Builds the named drills from a <see cref="ScenarioLibrary"/>. Each drill is a deterministic
/// selection over the shared content (optionally biased toward the player's weakest pattern), so the
/// same inputs always produce the same drill - which keeps it unit-testable. Selection criteria are
/// honest proxies from the data we have (theme tags, pattern id, material balance and move number
/// parsed from the FEN); there is no engine in the core, so "defender"/"capitalization" are read
/// from material, not evaluation.
/// </summary>
public static class DrillFactory
{
    public static Drill Build(DrillKind kind, ScenarioLibrary library, int count = 10, PatternId? weakest = null)
    {
        if (count < 1) count = 1;
        var all = library.AllScenarios.ToList();

        IEnumerable<Scenario> pool = kind switch
        {
            DrillKind.CheckmatePatterns => all.Where(IsMate),
            DrillKind.AdvantageCapitalization => all.Where(s => HasTheme(s, "advantage") || HasTheme(s, "crushing")),
            DrillKind.Defender => all.Where(s => MaterialBalance(s.Fen) < 0),
            DrillKind.OpeningImprover => all.Where(s => FullMove(s.Fen) <= 10),
            _ => all, // TimeTrainer, Intuition, BlunderPreventer: any position, varied
        };

        var chosen = Select(pool.ToList(), all, count, weakest, kind);
        return new Drill(kind, Title(kind), Description(kind), chosen);
    }

    /// <summary>Generate two-choice "pick the stronger move" problems for the Blunder Preventer drill.</summary>
    public static IReadOnlyList<TwoChoiceProblem> BlunderPreventer(ScenarioLibrary library, int count = 10)
    {
        var problems = new List<TwoChoiceProblem>();
        foreach (var s in Build(DrillKind.BlunderPreventer, library, count * 2).Scenarios)
        {
            if (s.Solutions.Count == 0) continue;
            string better = s.Solutions[0];
            string? worse = PlausibleAlternative(s.Fen, s.Solutions);
            if (worse is null) continue;

            // Fixed, deterministic display order so tests and UI agree: alphabetical by UCI.
            var options = new[] { better, worse }.OrderBy(m => m, StringComparer.Ordinal).ToList();
            problems.Add(new TwoChoiceProblem(s.Fen, s.Prompt, better, worse, options));
            if (problems.Count >= count) break;
        }
        return problems;
    }

    // -- selection ----------------------------------------------------------

    private static IReadOnlyList<Scenario> Select(
        List<Scenario> pool, List<Scenario> all, int count, PatternId? weakest, DrillKind kind)
    {
        if (pool.Count == 0)
            pool = all; // never hand back an empty drill; fall back to the whole library

        // Bias to the weakest pattern first, then spread across patterns for variety.
        var ordered = pool
            .OrderByDescending(s => weakest.HasValue && s.Pattern.Value == weakest.Value.Value)
            .ThenBy(s => StableHash(s.Id))
            .ToList();

        if (kind is DrillKind.TimeTrainer or DrillKind.Intuition)
            ordered = SpreadByPattern(ordered);

        return ordered.Take(count).ToList();
    }

    // Round-robin one scenario per pattern before repeating, for a varied-feeling set.
    private static List<Scenario> SpreadByPattern(List<Scenario> ordered)
    {
        var byPattern = new Dictionary<string, Queue<Scenario>>();
        foreach (var s in ordered)
        {
            if (!byPattern.TryGetValue(s.Pattern.Value, out var q))
                byPattern[s.Pattern.Value] = q = new Queue<Scenario>();
            q.Enqueue(s);
        }
        var patterns = byPattern.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var result = new List<Scenario>();
        bool any = true;
        while (any)
        {
            any = false;
            foreach (var p in patterns)
                if (byPattern[p].Count > 0) { result.Add(byPattern[p].Dequeue()); any = true; }
        }
        return result;
    }

    // -- honest FEN-derived reads -------------------------------------------

    private static bool IsMate(Scenario s) =>
        s.Pattern.Value.StartsWith("checkmate.", StringComparison.Ordinal)
        || HasTheme(s, "mate") || HasTheme(s, "mateIn1") || HasTheme(s, "mateIn2") || HasTheme(s, "mateIn3");

    private static bool HasTheme(Scenario s, string theme) =>
        s.ThemeTags.Any(t => string.Equals(t, theme, StringComparison.OrdinalIgnoreCase));

    // Material from the side-to-move's point of view: positive = up material, negative = down.
    private static int MaterialBalance(string fen)
    {
        var parts = fen.Split(' ');
        bool whiteToMove = parts.Length < 2 || parts[1] == "w";
        int white = 0, black = 0;
        foreach (char c in parts[0])
        {
            int v = char.ToUpperInvariant(c) switch { 'P' => 1, 'N' => 3, 'B' => 3, 'R' => 5, 'Q' => 9, _ => 0 };
            if (v == 0) continue;
            if (char.IsUpper(c)) white += v; else black += v;
        }
        int diff = white - black;
        return whiteToMove ? diff : -diff;
    }

    private static int FullMove(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length >= 6 && int.TryParse(parts[5], out int n) ? n : 1;
    }

    private static string? PlausibleAlternative(string fen, IReadOnlyList<string> solutions)
    {
        ChessGame game;
        try { game = ChessGame.FromFen(fen); }
        catch { return null; }

        foreach (var m in game.LegalUciMoves().OrderBy(m => m, StringComparer.Ordinal))
            if (!solutions.Contains(m))
                return m;
        return null;
    }

    // Stable string hash (FNV-1a) - unlike string.GetHashCode this is deterministic across runs.
    private static uint StableHash(string s)
    {
        uint h = 2166136261u;
        foreach (char c in s) { h ^= c; h *= 16777619u; }
        return h;
    }

    /// <summary>The drill's display title, without building the (potentially large) scenario set.</summary>
    public static string TitleOf(DrillKind k) => Title(k);

    /// <summary>The drill's one-line description, without building the scenario set.</summary>
    public static string DescriptionOf(DrillKind k) => Description(k);

    // -- copy ----------------------------------------------------------------

    private static string Title(DrillKind k) => k switch
    {
        DrillKind.TimeTrainer => "Time Trainer",
        DrillKind.Intuition => "Intuition",
        DrillKind.Defender => "Defender",
        DrillKind.AdvantageCapitalization => "Advantage Capitalization",
        DrillKind.BlunderPreventer => "Blunder Preventer",
        DrillKind.CheckmatePatterns => "Checkmate Patterns",
        DrillKind.OpeningImprover => "Opening Improver",
        _ => k.ToString(),
    };

    private static string Description(DrillKind k) => k switch
    {
        DrillKind.TimeTrainer => "Find the best move against the clock across varied positions.",
        DrillKind.Intuition => "Trust your first instinct - answer quickly, then check.",
        DrillKind.Defender => "You are worse. Find the move that holds the position together.",
        DrillKind.AdvantageCapitalization => "You are winning. Convert the advantage without slipping.",
        DrillKind.BlunderPreventer => "Two candidate moves - pick the stronger one.",
        DrillKind.CheckmatePatterns => "Timed mate flashcards: spot the finish.",
        DrillKind.OpeningImprover => "Clean up the mistakes that show up early in your games.",
        _ => "",
    };
}
