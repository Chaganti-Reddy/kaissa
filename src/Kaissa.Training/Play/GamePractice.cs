using Kaissa.Training.Import;

namespace Kaissa.Training.Play;

/// <summary>
/// Turns the mistakes a player made in a game into practice scenarios: each becomes a position
/// with the move they missed as the solution. The missed move's motif routes the scenario to the
/// matching pattern (a missed fork trains the fork pattern), so playing sharpens the exact skill.
/// Motifs that cannot be classified precisely go under a "from your games" pattern.
/// </summary>
public static class GamePractice
{
    /// <summary>Catch-all pattern for game mistakes whose motif is not precisely classified.</summary>
    public static Pattern FromYourGames { get; } = new(
        new PatternId("tactic.from_your_games"),
        "From your games",
        "A position where you went wrong - find the move you missed.");

    private static readonly PatternId ForkPattern = new("tactic.fork");
    private static readonly PatternId HangingPattern = new("tactic.hanging_piece");
    private static readonly PatternId PinPattern = new("tactic.pin");
    private static readonly PatternId SkewerPattern = new("tactic.skewer");
    private static readonly PatternId DiscoveredPattern = new("tactic.discovered_attack");
    private static readonly PatternId DoubleCheckPattern = new("tactic.double_check");
    private static readonly PatternId BackRankPattern = new("checkmate.back_rank");
    private static readonly PatternId SmotheredPattern = new("checkmate.smothered");

    /// <summary>The full pattern (with metadata) for a game-practice scenario's pattern id.</summary>
    public static Pattern PatternFor(PatternId id) =>
        id == FromYourGames.Id ? FromYourGames : LichessPuzzleParser.Catalog[id];

    public static IReadOnlyList<Scenario> FromAssessments(
        IEnumerable<MoveAssessment> assessments, int rating = 1200, MoveQuality worseThan = MoveQuality.Inaccuracy)
    {
        return assessments
            .Where(a => a.Quality > worseThan)
            .Select(a =>
            {
                var pattern = PatternForMotif(MotifClassifier.Classify(a.Fen, a.BestMove));
                return new Scenario(
                    $"yourgame-{a.Ply}",
                    pattern,
                    a.Fen,
                    new[] { a.BestMove },
                    "You went wrong here. Find the move you missed.",
                    rating);
            })
            .ToList();
    }

    private static PatternId PatternForMotif(Motif motif) => motif switch
    {
        Motif.Fork => ForkPattern,
        Motif.DiscoveredAttack => DiscoveredPattern,
        Motif.Pin => PinPattern,
        Motif.Skewer => SkewerPattern,
        Motif.HangingPiece => HangingPattern,
        Motif.DoubleCheck => DoubleCheckPattern,
        Motif.BackRankMate => BackRankPattern,
        Motif.SmotheredMate => SmotheredPattern,
        _ => FromYourGames.Id, // generic checkmate and anything unclassified
    };
}
