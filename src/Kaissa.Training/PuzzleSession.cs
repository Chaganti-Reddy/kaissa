using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>What happened when the player submitted a move in a puzzle.</summary>
public enum PuzzleOutcome
{
    /// <summary>Wrong move - the position is unchanged; the player may retry.</summary>
    Wrong,

    /// <summary>Correct move, but the puzzle continues (an opponent reply was played).</summary>
    Continue,

    /// <summary>Correct move that completed the puzzle.</summary>
    Solved,
}

public sealed record PuzzleMoveResult(
    PuzzleOutcome Outcome,
    string PlayerMove,     // the player's move, in canonical UCI (as applied)
    string ExpectedMove,   // the correct solver move at this step (for hint/solution/feedback)
    string? ReplyMove,     // opponent's auto-reply UCI that was played, or null
    string FenAfterPlayer, // position right after the player's move (before the reply)
    string FenAfterReply); // position after the reply too (== FenAfterPlayer if none / wrong)

/// <summary>
/// Drives one multi-move puzzle. The player must find each solver move in <see cref="Scenario.SolverLine"/>;
/// the opponent's replies are played automatically. Pure logic (no Unity), so it is unit-testable and
/// portable. A wrong move leaves the position untouched so the caller can snap the piece back and let
/// the player try again. Any move that delivers checkmate is accepted (covers alternative mates).
/// </summary>
public sealed class PuzzleSession
{
    private readonly Scenario _scenario;
    private readonly IReadOnlyList<string> _line;
    private ChessGame _game;
    private int _index; // count of line plies played so far; even => a solver move is expected next

    public PuzzleSession(Scenario scenario)
    {
        _scenario = scenario;
        _line = scenario.SolverLine;
        _game = ChessGame.FromFen(scenario.Fen);
        _index = 0;
    }

    /// <summary>Builds a session from raw puzzle data (e.g. a UI card) without a full Scenario.</summary>
    public PuzzleSession(string fen, IReadOnlyList<string> line, string? setup = null)
        : this(new Scenario("card", new PatternId("adhoc"), fen,
            line is { Count: > 0 } ? new[] { line[0] } : Array.Empty<string>(),
            "", 0, Line: line, Setup: setup))
    {
    }

    /// <summary>The starting position (solver to move), for the initial render.</summary>
    public string StartFen => _scenario.Fen;

    public string Fen => _game.Fen;

    /// <summary>The opponent's setup move that led into <see cref="StartFen"/>, if any (for the load animation).</summary>
    public string? SetupMove => _scenario.Setup;

    public bool Solved => _index >= _line.Count;

    /// <summary>The solver move expected right now, in canonical UCI, or null if solved.</summary>
    public string? ExpectedMove =>
        _index < _line.Count ? Canonical(_line[_index]) ?? _line[_index] : null;

    /// <summary>Total solver moves the player must find.</summary>
    public int SolverMovesTotal => (_line.Count + 1) / 2;

    /// <summary>Solver moves correctly played so far.</summary>
    public int SolverMovesDone => (_index + 1) / 2;

    /// <summary>Grades and (if correct) applies a solver move plus the scripted opponent reply.</summary>
    public PuzzleMoveResult Submit(string uci)
    {
        if (Solved)
            return Miss(uci);

        var expectedRaw = _line[_index];
        var expected = Canonical(expectedRaw) ?? expectedRaw;
        var played = Canonical(uci) ?? uci;

        bool correct = MovesEqual(played, expected);

        // Accept any legal move that gives immediate checkmate, even off the scripted line.
        if (!correct && DeliversMate(uci))
        {
            _game.TryMakeMove(uci);
            _index = _line.Count;
            return new PuzzleMoveResult(PuzzleOutcome.Solved, played, expected, null, _game.Fen, _game.Fen);
        }

        if (!correct)
            return Miss(uci);

        if (!_game.TryMakeMove(expectedRaw))
            return Miss(uci);
        var fenAfterPlayer = _game.Fen;
        _index++;

        if (_index < _line.Count)
        {
            var reply = _line[_index];
            if (_game.TryMakeMove(reply))
            {
                _index++;
                var replyUci = Canonical(reply) ?? reply;
                var outcome = _index >= _line.Count ? PuzzleOutcome.Solved : PuzzleOutcome.Continue;
                return new PuzzleMoveResult(outcome, expected, expected, replyUci, fenAfterPlayer, _game.Fen);
            }
            // Reply illegal (bad data) - treat the puzzle as solved rather than wedging.
            _index = _line.Count;
        }

        return new PuzzleMoveResult(PuzzleOutcome.Solved, expected, expected, null, fenAfterPlayer, fenAfterPlayer);
    }

    private PuzzleMoveResult Miss(string uci)
    {
        var expected = ExpectedMove ?? "";
        return new PuzzleMoveResult(PuzzleOutcome.Wrong, uci, expected, null, _game.Fen, _game.Fen);
    }

    private bool DeliversMate(string uci)
    {
        try
        {
            var probe = ChessGame.FromFen(_game.Fen);
            return probe.TryMakeMove(uci) && probe.IsCheckmate;
        }
        catch { return false; }
    }

    private string? Canonical(string move)
    {
        try { return _game.ResolveToUci(move); }
        catch { return null; }
    }

    private static bool MovesEqual(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
