using Kaissa.Chess.Rules;

namespace Kaissa.Training.Api;

/// <summary>
/// The navigable state of an analysis board: a single line of moves from a starting position that the
/// player can extend, step back and forth through, branch from, or replace by loading a FEN. Engine
/// evaluation of the current position is a separate concern (see <see cref="KaissaAnalysis"/>); this
/// type is pure and synchronous so the navigation can be unit-tested without an engine.
/// </summary>
public sealed class AnalysisSession
{
    private string _startFen;
    private readonly List<string> _moves = new(); // the current line, in UCI
    private int _index;                            // how many of _moves are applied (0.._moves.Count)

    public AnalysisSession(string? startFen = null) =>
        _startFen = startFen ?? ChessGame.Start().Fen;

    public string StartFen => _startFen;

    /// <summary>How many moves deep the current position is.</summary>
    public int Ply => _index;

    /// <summary>Total moves in the current line (may be more than <see cref="Ply"/> after stepping back).</summary>
    public int LineLength => _moves.Count;

    public bool CanStepBack => _index > 0;
    public bool CanStepForward => _index < _moves.Count;

    /// <summary>FEN of the position at the current point in the line.</summary>
    public string CurrentFen
    {
        get
        {
            var game = ChessGame.FromFen(_startFen);
            for (int i = 0; i < _index; i++)
                game.TryMakeMove(_moves[i]);
            return game.Fen;
        }
    }

    public bool WhiteToMove => ChessGame.FromFen(CurrentFen).SideToMove == Side.White;
    public bool IsGameOver => ChessGame.FromFen(CurrentFen).IsGameOver;

    /// <summary>The current line in standard algebraic notation, from the start position.</summary>
    public IReadOnlyList<string> LineSan()
    {
        var game = ChessGame.FromFen(_startFen);
        var san = new List<string>(_moves.Count);
        foreach (var move in _moves)
        {
            san.Add(game.SanForUci(move) ?? move);
            game.TryMakeMove(move);
        }
        return san;
    }

    /// <summary>
    /// Plays a legal move (SAN or UCI) from the current position. If we had stepped back, the moves
    /// ahead are discarded and this move starts a new continuation. Returns false for an illegal move.
    /// </summary>
    public bool Play(string move)
    {
        var game = ChessGame.FromFen(CurrentFen);
        var uci = game.ResolveToUci(move);
        if (uci is null || !game.TryMakeMove(uci))
            return false;

        if (_index < _moves.Count)
            _moves.RemoveRange(_index, _moves.Count - _index); // branch: drop the old continuation
        _moves.Add(uci);
        _index++;
        return true;
    }

    public bool StepBack()
    {
        if (_index == 0)
            return false;
        _index--;
        return true;
    }

    public bool StepForward()
    {
        if (_index >= _moves.Count)
            return false;
        _index++;
        return true;
    }

    public void GoToStart() => _index = 0;
    public void GoToEnd() => _index = _moves.Count;

    /// <summary>Jumps to a specific ply in the current line (clamped to the line's length).</summary>
    public void GoToPly(int ply) => _index = Math.Clamp(ply, 0, _moves.Count);

    /// <summary>Plays a whole sequence of moves (UCI/SAN) from the current point; stops at the first illegal one.</summary>
    public void PlayLine(IEnumerable<string> moves)
    {
        foreach (var m in moves)
            if (!Play(m)) break;
    }

    /// <summary>Replaces the whole session with a fresh position from a FEN.</summary>
    public void LoadFen(string fen)
    {
        _startFen = fen;
        _moves.Clear();
        _index = 0;
    }
}
