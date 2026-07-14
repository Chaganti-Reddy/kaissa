namespace Kaissa.Training.Api;

/// <summary>Result of the player submitting a move in a game (and the bot's reply, if any).</summary>
public sealed record MoveOutcome(
    bool Accepted,
    string? PlayerMove,
    string? BotMove,
    BoardView Board,
    bool IsGameOver,
    string Result);

/// <summary>One reviewed move from a post-game review.</summary>
public sealed record GameReviewItem(
    int MoveNumber,
    string Side,           // "White" or "Black"
    string PlayedMove,     // UCI
    string PlayedMoveSan,
    string BestMove,       // UCI
    string BestMoveSan,
    string Quality,
    int CentipawnLoss,
    string Commentary);

/// <summary>
/// A post-game review: per-move assessment for both sides, accuracy for each player, the opening
/// played and how far it stayed in book, the turning points, a single-game performance estimate, and
/// the practice positions generated from the player's mistakes (each tagged with the missed motif).
/// </summary>
public sealed record GameReviewResult(
    IReadOnlyList<GameReviewItem> Mistakes,
    IReadOnlyList<Scenario> Practice,
    double Accuracy,
    double OpponentAccuracy,
    IReadOnlyList<int> EvalSeries,   // player-perspective centipawns after each ply (the graph curve)
    Kaissa.Training.Play.PhaseAccuracy PhaseAccuracy,
    IReadOnlyList<GameReviewItem> AllMoves,    // every move, both sides
    IReadOnlyList<GameReviewItem> KeyMoments,  // the biggest turning points
    string OpeningName,
    string OpeningEco,
    int BookUntilMove,               // last full-move number that was still opening theory (0 = none)
    int PerformanceRating,
    IReadOnlyList<int> TacticsFound,   // player's tactics taken, indexed [fork, pin, mate, hanging]
    IReadOnlyList<int> TacticsMissed); // tactics that were there and the player did not play
