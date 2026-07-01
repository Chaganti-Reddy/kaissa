namespace Kaissa.Training.Api;

/// <summary>Result of the player submitting a move in a game (and the bot's reply, if any).</summary>
public sealed record MoveOutcome(
    bool Accepted,
    string? PlayerMove,
    string? BotMove,
    BoardView Board,
    bool IsGameOver,
    string Result);

/// <summary>One flagged move from a post-game review.</summary>
public sealed record GameReviewItem(
    int MoveNumber,
    string PlayedMove,
    string BestMove,
    string Quality,
    int CentipawnLoss);

/// <summary>
/// A post-game review: the player's notable mistakes and the practice positions generated from
/// them (each already tagged with the pattern of the missed move's motif).
/// </summary>
public sealed record GameReviewResult(
    IReadOnlyList<GameReviewItem> Mistakes,
    IReadOnlyList<Scenario> Practice);
