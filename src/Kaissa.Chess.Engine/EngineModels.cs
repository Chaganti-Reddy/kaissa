using System.Globalization;

namespace Kaissa.Chess.Engine;

/// <summary>Identity and advertised options reported by an engine during the UCI handshake.</summary>
public sealed record EngineIdentification(
    string Name,
    string Author,
    IReadOnlyList<UciOption> Options)
{
    /// <summary>True if the engine can cap its playing strength to a target Elo.</summary>
    public bool SupportsElo =>
        Options.Any(o => o.Name.Equals("UCI_Elo", StringComparison.OrdinalIgnoreCase));

    public UciOption? Option(string name) =>
        Options.FirstOrDefault(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A single option advertised by the engine (e.g. <c>option name UCI_Elo type spin ...</c>).</summary>
public sealed record UciOption(
    string Name,
    string Type,
    string? Default,
    string? Min,
    string? Max,
    IReadOnlyList<string> Vars)
{
    public static UciOption Parse(string line)
    {
        // Format: option name <name may contain spaces> type <type> [default X] [min X] [max X] [var A] [var B]...
        const string prefix = "option name ";
        var typeIdx = line.IndexOf(" type ", StringComparison.Ordinal);
        if (!line.StartsWith(prefix, StringComparison.Ordinal) || typeIdx < 0)
            return new UciOption(line, "unknown", null, null, null, Array.Empty<string>());

        var name = line[prefix.Length..typeIdx];
        var rest = line[(typeIdx + " type ".Length)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        string type = rest.Length > 0 ? rest[0] : "unknown";
        string? def = null, min = null, max = null;
        var vars = new List<string>();

        for (int i = 1; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "default" when i + 1 < rest.Length: def = rest[++i]; break;
                case "min" when i + 1 < rest.Length: min = rest[++i]; break;
                case "max" when i + 1 < rest.Length: max = rest[++i]; break;
                case "var" when i + 1 < rest.Length: vars.Add(rest[++i]); break;
            }
        }

        return new UciOption(name, type, def, min, max, vars);
    }
}

/// <summary>How strong the engine should play. Full strength unless a cap is set.</summary>
public sealed record StrengthSettings
{
    /// <summary>Target Elo, applied via <c>UCI_LimitStrength</c> + <c>UCI_Elo</c> when supported.</summary>
    public int? Elo { get; init; }

    /// <summary>Fallback skill level (0..20 for Stockfish) when Elo capping is not used.</summary>
    public int? SkillLevel { get; init; }

    public static StrengthSettings Full { get; } = new();
    public static StrengthSettings FromElo(int elo) => new() { Elo = elo };
    public static StrengthSettings FromSkillLevel(int level) => new() { SkillLevel = level };
}

/// <summary>Bounds a single search. At least one limit should be set; a depth default is applied otherwise.</summary>
public sealed record SearchLimits
{
    public int? Depth { get; init; }
    public TimeSpan? MoveTime { get; init; }
    public long? Nodes { get; init; }

    /// <summary>Number of principal variations to report (>= 1).</summary>
    public int MultiPv { get; init; } = 1;

    public static SearchLimits ToDepth(int depth, int multiPv = 1) => new() { Depth = depth, MultiPv = multiPv };
    public static SearchLimits ForTime(TimeSpan time, int multiPv = 1) => new() { MoveTime = time, MultiPv = multiPv };
}

public enum ScoreKind
{
    Centipawns,
    Mate,
}

/// <summary>An engine evaluation, from the side-to-move's perspective.</summary>
public readonly record struct Score(ScoreKind Kind, int Value)
{
    public static Score Centipawns(int cp) => new(ScoreKind.Centipawns, cp);
    public static Score Mate(int movesToMate) => new(ScoreKind.Mate, movesToMate);

    public override string ToString() => Kind == ScoreKind.Mate
        ? $"#{Value}"
        : (Value / 100.0).ToString("+0.00;-0.00", CultureInfo.InvariantCulture);
}

/// <summary>One principal variation from a search.</summary>
public sealed record PvLine(
    int MultiPvIndex,
    Score Score,
    int Depth,
    IReadOnlyList<string> Moves)
{
    public string BestMove => Moves.Count > 0 ? Moves[0] : "";
}

/// <summary>The outcome of a completed search.</summary>
public sealed record SearchResult(
    string BestMove,
    string? Ponder,
    IReadOnlyList<PvLine> Lines)
{
    /// <summary>Evaluation of the primary line, if the engine reported one.</summary>
    public Score? Evaluation => Lines.Count > 0 ? Lines[0].Score : null;
}
