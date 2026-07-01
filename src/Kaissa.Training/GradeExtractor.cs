using Kaissa.Chess.Rules;
using Kaissa.Learning;

namespace Kaissa.Training;

/// <summary>The graded result of a single attempt at a scenario.</summary>
public readonly record struct Attempt(bool Correct, Rating Rating);

/// <summary>
/// Turns a player's move into an FSRS grade, without ever asking the player to rate themselves.
/// v0 heuristic: a solution move is graded by how quickly it was found; a wrong move is a lapse.
/// Later versions can consult the engine to accept alternative moves that share the same idea.
/// </summary>
public sealed class GradeExtractor
{
    private readonly TimeSpan _fastThreshold;
    private readonly TimeSpan _slowThreshold;

    public GradeExtractor(TimeSpan? fastThreshold = null, TimeSpan? slowThreshold = null)
    {
        _fastThreshold = fastThreshold ?? TimeSpan.FromSeconds(3);
        _slowThreshold = slowThreshold ?? TimeSpan.FromSeconds(12);
    }

    public Attempt Grade(Scenario scenario, string move, TimeSpan thinkingTime)
    {
        var game = ChessGame.FromFen(scenario.Fen);
        var uci = game.ResolveToUci(move);

        bool correct = uci is not null &&
            scenario.Solutions.Any(s => string.Equals(s, uci, StringComparison.OrdinalIgnoreCase));

        if (!correct)
            return new Attempt(false, Rating.Again);

        var rating = thinkingTime <= _fastThreshold ? Rating.Easy
            : thinkingTime <= _slowThreshold ? Rating.Good
            : Rating.Hard;

        return new Attempt(true, rating);
    }
}
