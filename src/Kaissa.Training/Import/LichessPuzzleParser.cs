using Kaissa.Chess.Rules;

namespace Kaissa.Training.Import;

/// <summary>One row of the Lichess puzzle database, parsed into fields we care about.</summary>
public sealed record ImportedPuzzle(
    string PuzzleId,
    string Fen,
    IReadOnlyList<string> Moves,
    int Rating,
    IReadOnlyList<string> Themes);

/// <summary>
/// Parses the openly-licensed (CC0) Lichess puzzle database into <see cref="Scenario"/> objects.
///
/// Lichess format notes: the FEN is the position *before* the opponent's setup move. The first
/// move in <c>Moves</c> is that setup move; after applying it, the solver is to move and the
/// second move is the correct solution. We therefore build the scenario from the position *after*
/// the setup move, with the solver's first move as the accepted solution.
/// </summary>
public static class LichessPuzzleParser
{
    /// <summary>Patterns we import, with display metadata, keyed by id.</summary>
    public static IReadOnlyDictionary<PatternId, Pattern> Catalog { get; } = new[]
    {
        Pat("checkmate.back_rank", "Back-rank mate", "Mate the king trapped on its back rank behind its own pawns."),
        Pat("checkmate.smothered", "Smothered mate", "A knight mates a king hemmed in by its own pieces."),
        Pat("checkmate.mate_in_two", "Mate in two", "Force checkmate in two moves."),
        Pat("tactic.fork", "Fork", "One piece attacks two or more targets at once."),
        Pat("tactic.pin", "Pin", "A piece cannot move without exposing a more valuable one behind it."),
        Pat("tactic.skewer", "Skewer", "A valuable piece is forced to move, exposing a lesser one behind it."),
        Pat("tactic.discovered_attack", "Discovered attack", "Moving one piece unveils an attack from another."),
        Pat("tactic.double_check", "Double check", "Two pieces give check at once; the king must move."),
        Pat("tactic.deflection", "Deflection", "Force a defender away from a key square or piece."),
        Pat("tactic.attraction", "Attraction", "Lure an enemy piece onto a square where it can be exploited."),
        Pat("tactic.intermezzo", "In-between move", "Insert an unexpected move before the expected reply."),
        Pat("tactic.trapped_piece", "Trapped piece", "Win a piece that has no safe square to escape to."),
        Pat("tactic.promotion", "Promotion", "Promote a pawn, or use the threat of promotion."),
        Pat("tactic.hanging_piece", "Hanging piece", "Win an undefended or under-defended piece."),
        Pat("tactic.sacrifice", "Sacrifice", "Give up material for a decisive attack or gain."),
    }.ToDictionary(p => p.Id);

    // Lichess theme -> our pattern, in priority order (most specific first).
    private static readonly (string Theme, PatternId Pattern)[] ThemePriority =
    {
        ("backRankMate", new PatternId("checkmate.back_rank")),
        ("smotheredMate", new PatternId("checkmate.smothered")),
        ("mateIn2", new PatternId("checkmate.mate_in_two")),
        ("doubleCheck", new PatternId("tactic.double_check")),
        ("discoveredAttack", new PatternId("tactic.discovered_attack")),
        ("skewer", new PatternId("tactic.skewer")),
        ("pin", new PatternId("tactic.pin")),
        ("fork", new PatternId("tactic.fork")),
        ("deflection", new PatternId("tactic.deflection")),
        ("attraction", new PatternId("tactic.attraction")),
        ("intermezzo", new PatternId("tactic.intermezzo")),
        ("trappedPiece", new PatternId("tactic.trapped_piece")),
        ("promotion", new PatternId("tactic.promotion")),
        ("hangingPiece", new PatternId("tactic.hanging_piece")),
        ("sacrifice", new PatternId("tactic.sacrifice")),
    };

    public static bool TryParseRow(string line, out ImportedPuzzle puzzle)
    {
        puzzle = null!;
        var f = line.Split(',');
        // PuzzleId,FEN,Moves,Rating,RatingDeviation,Popularity,NbPlays,Themes,GameUrl,OpeningTags
        if (f.Length < 8)
            return false;
        if (!int.TryParse(f[3], out var rating))
            return false;

        var moves = f[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var themes = f[7].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        puzzle = new ImportedPuzzle(f[0], f[1], moves, rating, themes);
        return true;
    }

    /// <summary>Maps a puzzle's themes to one primary pattern, or null if none are supported.</summary>
    public static PatternId? MapPattern(IReadOnlyList<string> themes)
    {
        foreach (var (theme, pattern) in ThemePriority)
        {
            if (themes.Contains(theme))
                return pattern;
        }

        return null;
    }

    /// <summary>Builds a scenario from a puzzle, or returns false with a reason if it is unusable.</summary>
    public static bool TryBuildScenario(ImportedPuzzle puzzle, out Scenario scenario, out string reason)
    {
        scenario = null!;
        reason = "";

        if (puzzle.Moves.Count < 2)
        {
            reason = "not enough moves";
            return false;
        }

        var pattern = MapPattern(puzzle.Themes);
        if (pattern is not { } patternId)
        {
            reason = "no supported theme";
            return false;
        }

        ChessGame game;
        try
        {
            game = ChessGame.FromFen(puzzle.Fen);
        }
        catch (Exception)
        {
            reason = "invalid FEN";
            return false;
        }

        var setup = game.ResolveToUci(puzzle.Moves[0]);
        if (setup is null || !game.TryMakeMove(puzzle.Moves[0]))
        {
            reason = "setup move illegal";
            return false;
        }

        var startFen = game.Fen;

        // Walk and canonicalise the whole solver/opponent line, validating each ply as we go.
        var line = new List<string>(puzzle.Moves.Count - 1);
        for (int i = 1; i < puzzle.Moves.Count; i++)
        {
            var uci = game.ResolveToUci(puzzle.Moves[i]);
            if (uci is null || !game.TryMakeMove(puzzle.Moves[i]))
            {
                reason = $"line move {i} illegal";
                return false;
            }
            line.Add(uci);
        }

        scenario = new Scenario(
            $"lichess-{puzzle.PuzzleId}",
            patternId,
            startFen,
            new[] { line[0] },
            $"{PlayerSide(startFen)} to move. Find the best move.",
            puzzle.Rating,
            Line: line,
            Themes: puzzle.Themes,
            Setup: setup);
        return true;
    }

    private static string PlayerSide(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length > 1 && parts[1] == "b" ? "Black" : "White";
    }

    private static Pattern Pat(string id, string name, string description) =>
        new(new PatternId(id), name, description);
}
