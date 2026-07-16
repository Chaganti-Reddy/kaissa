using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>What the player must achieve in an endgame drill.</summary>
public enum DrillGoal { Win, Draw, Promote }

/// <summary>How a drill is going right now.</summary>
public enum DrillOutcome { Ongoing, Passed, Failed }

/// <summary>A named endgame position to learn by playing it out against the engine.</summary>
public sealed record EndgamePosition(
    string Id, string Category, string Name, string Fen, DrillGoal Goal, string Note)
{
    /// <summary>The side the player controls (the side to move in the FEN).</summary>
    public bool PlayerWhite
    {
        get { var p = Fen.Split(' '); return p.Length < 2 || p[1] != "b"; }
    }

    /// <summary>A short human goal line for the UI.</summary>
    public string GoalText => Goal switch
    {
        DrillGoal.Win => "Win for " + (PlayerWhite ? "White" : "Black"),
        DrillGoal.Draw => "Hold the draw",
        DrillGoal.Promote => "Promote the pawn",
        _ => "",
    };
}

/// <summary>
/// A curated catalogue of instructive endgames, grouped by category. The trainer plays one out against
/// the engine on the Endgames page and grades it with <see cref="DrillEvaluator"/>. Every FEN is a legal
/// position (asserted by the tests); positions are chosen to teach a concrete technique.
/// </summary>
public static class EndgameLibrary
{
    public static IReadOnlyList<EndgamePosition> All { get; } = new[]
    {
        new EndgamePosition("mate_kq", "Checkmates", "Queen mate",
            "8/8/8/4k3/8/8/2Q5/4K3 w - - 0 1", DrillGoal.Win,
            "Box the king in with the queen a knight's move away, bring your king up, then mate."),
        new EndgamePosition("mate_kr", "Checkmates", "Rook mate",
            "4k3/8/8/8/8/8/8/R3K3 w - - 0 1", DrillGoal.Win,
            "Cut the king off with the rook and use your king to shoulder it to the edge."),
        new EndgamePosition("mate_krr", "Checkmates", "Two-rook (ladder) mate",
            "4k3/8/8/8/8/8/8/R2RK3 w - - 0 1", DrillGoal.Win,
            "Walk the king down the board with alternating rook checks - the ladder."),
        new EndgamePosition("mate_kbb", "Checkmates", "Two-bishop mate",
            "4k3/8/8/8/8/8/8/2B1KB2 w - - 0 1", DrillGoal.Win,
            "The bishops build a wall; drive the king into a corner with the king's help."),
        new EndgamePosition("mate_kbn", "Checkmates", "Bishop and knight mate",
            "4k3/8/8/8/8/8/8/2BNK3 w - - 0 1", DrillGoal.Win,
            "The hardest basic mate: force the king to a corner the bishop controls, knight and king herding."),
        new EndgamePosition("q_vs_r", "Queen", "Queen versus rook",
            "4k3/3r4/8/8/8/8/8/Q3K3 w - - 0 1", DrillGoal.Win,
            "Pin or fork the rook away from its king; keep checking until it drops or the king is mated."),

        new EndgamePosition("kp_opposition", "King & Pawn", "The opposition",
            "8/8/8/4k3/8/4K3/4P3/8 w - - 0 1", DrillGoal.Win,
            "Take the opposition to force the enemy king aside and escort the pawn home."),
        new EndgamePosition("kp_vs_k", "King & Pawn", "King and pawn vs king",
            "8/8/8/3k4/8/3K4/3P4/8 w - - 0 1", DrillGoal.Win,
            "Keep your king in front of the pawn and seize the key squares."),
        new EndgamePosition("kp_promote", "King & Pawn", "Promotion",
            "8/4P3/4k3/8/8/8/8/4K3 w - - 0 1", DrillGoal.Promote,
            "Shepherd the pawn to the eighth rank and promote."),

        new EndgamePosition("kp_two", "King & Pawn", "Two connected pawns",
            "8/8/8/3k4/8/3K4/2PP4/8 w - - 0 1", DrillGoal.Win,
            "Advance the connected pawns side by side, each guarding the other's square, and promote."),

        new EndgamePosition("mp_bishop", "Minor Piece", "Bishop and pawn",
            "8/8/8/4k3/8/4K3/3BP3/8 w - - 0 1", DrillGoal.Win,
            "Escort the pawn with the king and bishop; the bishop covers the promotion square."),
        new EndgamePosition("mp_knight", "Minor Piece", "Knight and pawn",
            "8/8/8/4k3/8/4K1N1/4P3/8 w - - 0 1", DrillGoal.Win,
            "Shepherd the pawn with the king while the knight controls key squares, then promote."),

        new EndgamePosition("rk_lucena", "Rook", "Win with the Lucena",
            "1K1k4/1P6/8/8/8/8/r7/2R5 w - - 0 1", DrillGoal.Win,
            "Build a bridge: shield your king from the checks with the rook, then escort the pawn in."),
        new EndgamePosition("rk_hold", "Rook", "Hold the rook-endgame draw",
            "3k4/8/3K4/3P4/8/8/1r6/3R4 b - - 0 1", DrillGoal.Draw,
            "King in front of the pawn, rook active from behind - a single extra pawn cannot break through."),

        new EndgamePosition("q_vs_p", "Queen", "Queen vs pawn",
            "8/8/8/8/8/1k6/1p6/4K1Q1 w - - 0 1", DrillGoal.Win,
            "Approach with checks that gain a tempo to stop the pawn, then win it."),
    };

    public static EndgamePosition? ById(string id) => All.FirstOrDefault(p => p.Id == id);

    /// <summary>Categories in display order.</summary>
    public static IReadOnlyList<string> Categories { get; } =
        All.Select(e => e.Category).Distinct().ToList();

    public static IReadOnlyList<EndgamePosition> InCategory(string category) =>
        All.Where(e => e.Category == category).ToList();
}

/// <summary>Grades an endgame drill from the current game state.</summary>
public static class DrillEvaluator
{
    /// <summary>
    /// Evaluate a drill. <paramref name="fen"/> is the current position; <paramref name="playerWhite"/>
    /// is the side the player controls. Win/Promote pass when the player mates or promotes; a drawn or
    /// lost position fails them. Draw passes on any draw and fails only if the player is checkmated.
    /// </summary>
    public static DrillOutcome Evaluate(string fen, bool playerWhite, DrillGoal goal)
    {
        ChessGame game;
        try { game = ChessGame.FromFen(fen); }
        catch { return DrillOutcome.Ongoing; }

        if (goal == DrillGoal.Promote && HasQueen(fen, playerWhite))
            return DrillOutcome.Passed;

        if (!game.IsGameOver)
            return DrillOutcome.Ongoing;

        if (game.IsCheckmate)
        {
            // The side to move is the one that has been checkmated.
            bool playerMated = (game.SideToMove == Side.White) == playerWhite;
            return playerMated ? DrillOutcome.Failed : DrillOutcome.Passed;
        }

        // Game over but not checkmate => a draw (stalemate / insufficient material / etc.).
        return goal == DrillGoal.Draw ? DrillOutcome.Passed : DrillOutcome.Failed;
    }

    private static bool HasQueen(string fen, bool white)
    {
        var placement = fen.Split(' ')[0];
        return placement.IndexOf(white ? 'Q' : 'q') >= 0;
    }
}
