using Kaissa.Chess.Rules;

namespace Kaissa.Training.Play;

/// <summary>
/// A short, plain one-line explanation of a move for the post-game review, in the spirit of a coach's
/// note. Templated (no natural-language model): keyed off the move's quality, the motif of the move
/// that should have been played, and the opening name while still in book.
/// </summary>
public static class MoveCommentary
{
    public static string Describe(MoveAssessment a, Motif bestMotif, string? openingName)
    {
        string best = a.BestMoveSan;
        return a.Quality switch
        {
            MoveQuality.Book => string.IsNullOrEmpty(openingName) ? "A standard opening move." : $"Book: {openingName}.",
            MoveQuality.Brilliant => "Brilliant - a sacrifice that keeps the advantage.",
            MoveQuality.Great => "Great - practically the only move that holds.",
            MoveQuality.Best => "Best move.",
            MoveQuality.Excellent => "Excellent, almost the top choice.",
            MoveQuality.Good => "A reasonable move.",
            MoveQuality.Inaccuracy => $"Inaccurate - {best} was stronger.",
            MoveQuality.Miss => $"Missed {MotifPhrase(bestMotif)} with {best}.",
            MoveQuality.Mistake => $"Mistake - {best} was much better.",
            MoveQuality.Blunder => $"Blunder - {best} was needed.",
            _ => "",
        };
    }

    private static string MotifPhrase(Motif m) => m switch
    {
        Motif.Fork => "a fork",
        Motif.Pin => "a pin",
        Motif.Skewer => "a skewer",
        Motif.DiscoveredAttack => "a discovered attack",
        Motif.DoubleCheck => "a double check",
        Motif.HangingPiece => "a free piece",
        Motif.BackRankMate => "a back-rank mate",
        Motif.SmotheredMate => "a smothered mate",
        Motif.Checkmate => "a forced mate",
        _ => "a stronger continuation",
    };
}
