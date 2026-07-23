using System.Collections.Generic;
using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>A short, public-domain master game for the guess-the-move trainer. Moves are in UCI from the
/// start; <see cref="PlayerSide"/> is the side the solver predicts (the other side plays automatically).</summary>
public sealed record MasterGame(string Id, string White, string Black, string Event, int Year, Side PlayerSide, IReadOnlyList<string> Moves);

/// <summary>A small bundled set of famous, out-of-copyright games (game scores are facts, freely usable).</summary>
public static class MasterGames
{
    public static IReadOnlyList<MasterGame> All { get; } = new[]
    {
        new MasterGame("opera", "Morphy", "Duke Karl / Count Isouard", "Paris", 1858, Side.White, new[]
        {
            "e2e4","e7e5","g1f3","d7d6","d2d4","c8g4","d4e5","g4f3","d1f3","d6e5","f1c4","g8f6",
            "f3b3","d8e7","b1c3","c7c6","c1g5","b7b5","c3b5","c6b5","c4b5","b8d7","e1c1","a8d8",
            "d1d7","d8d7","h1d1","e7e6","b5d7","f6d7","b3b8","d7b8","d1d8",
        }),
        new MasterGame("legal", "De Legal", "Saint Brie", "Paris", 1750, Side.White, new[]
        {
            "e2e4","e7e5","f1c4","d7d6","g1f3","c8g4","b1c3","g7g6","f3e5","g4d1","c4f7","e8e7","c3d5",
        }),
        new MasterGame("scholar", "Attacker", "Defender", "Illustration", 1600, Side.White, new[]
        {
            "e2e4","e7e5","f1c4","b8c6","d1h5","g8f6","h5f7",
        }),
        new MasterGame("reti_tartakower", "Reti", "Tartakower", "Vienna", 1910, Side.White, new[]
        {
            "e2e4","c7c6","d2d4","d7d5","b1c3","d5e4","c3e4","g8f6","d1d3","e7e5","d4e5","d8a5",
            "c1d2","a5e5","e1c1","f6e4","d3d8","e8d8","d2g5","d8e8","d1d8",
        }),
    };

    public static MasterGame? ById(string id)
    {
        foreach (var g in All) if (g.Id == id) return g;
        return null;
    }
}

/// <summary>The result of guessing one move: whether it matched the master, the master's actual move (SAN),
/// and whether the game is now over.</summary>
public sealed record GuessResult(bool Correct, string ActualUci, string ActualSan, bool Done);

/// <summary>
/// Guess-the-move over a master game: the solver predicts each of their side's moves; the opponent's moves
/// play automatically. A guess scores when it matches the master's move; either way the real game move is
/// then played so the solver always follows the actual game. Scoring is by the master's move (offline, no
/// engine) - a different-but-good move is not credited, matching the simplest form of the drill.
/// </summary>
public sealed class GuessMoveSession
{
    private readonly MasterGame _game;
    private ChessGame _board;
    private int _ply;

    public GuessMoveSession(MasterGame game)
    {
        _game = game;
        _board = ChessGame.Start();
        AdvanceOpponent();
    }

    public string Fen => _board.Fen;
    public int Score { get; private set; }
    public int Answered { get; private set; }
    public bool Done => _ply >= _game.Moves.Count;
    public bool PlayerToMove => !Done && _board.SideToMove == _game.PlayerSide;

    /// <summary>Total moves the player is asked to guess in this game.</summary>
    public int TotalGuesses
    {
        get
        {
            var g = ChessGame.Start();
            int count = 0;
            foreach (var mv in _game.Moves)
            {
                if (g.SideToMove == _game.PlayerSide) count++;
                if (!g.TryMakeMove(mv)) break;
            }
            return count;
        }
    }

    /// <summary>Grades a guessed move against the master's move for the current ply, then plays the real
    /// game forward (the guessed-at move plus the opponent's reply).</summary>
    public GuessResult Guess(string uci)
    {
        if (Done) return new GuessResult(false, "", "", true);

        string actual = _game.Moves[_ply];
        bool correct = string.Equals(uci, actual, System.StringComparison.OrdinalIgnoreCase);
        string san = _board.SanForUci(actual) ?? actual;

        Answered++;
        if (correct) Score++;

        _board.TryMakeMove(actual);
        _ply++;
        AdvanceOpponent();

        return new GuessResult(correct, actual, san, Done);
    }

    // Auto-play the opponent's moves so the next prompt is always for the player's side.
    private void AdvanceOpponent()
    {
        while (!Done && _board.SideToMove != _game.PlayerSide)
        {
            if (!_board.TryMakeMove(_game.Moves[_ply])) { _ply = _game.Moves.Count; break; }
            _ply++;
        }
    }
}
