using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>
/// A single queued premove (Lichess allows one; chess.com stacks several - we start with one). The
/// player commits a move during the opponent's turn; when it becomes their turn again the move is
/// played if it is still legal, otherwise it is silently discarded. Holding only UCI keeps this
/// engine- and view-agnostic and unit-testable.
/// </summary>
public sealed class PremoveQueue
{
    private string? _pending;

    /// <summary>True while a premove is waiting to be applied.</summary>
    public bool HasPremove => _pending != null;

    /// <summary>The queued move in UCI, or null.</summary>
    public string? Pending => _pending;

    /// <summary>Queue a move (replacing any existing one). Rejects obviously malformed input.</summary>
    public bool Set(string? uci)
    {
        if (string.IsNullOrWhiteSpace(uci) || uci.Length is not (4 or 5))
            return false;
        _pending = uci.ToLowerInvariant();
        return true;
    }

    /// <summary>Discard the queued move (e.g. the player cancels, or the game ends).</summary>
    public void Clear() => _pending = null;

    /// <summary>
    /// Try to apply the queued move to the current position. Clears the queue either way. Returns the
    /// move to play when it is legal now, or null when there was nothing queued or it is no longer
    /// legal (the opponent's move changed the position underneath it).
    /// </summary>
    public string? Consume(ChessGame game)
    {
        var move = _pending;
        _pending = null;
        if (move is null)
            return null;
        foreach (var m in game.LegalUciMoves())
            if (string.Equals(m, move, StringComparison.OrdinalIgnoreCase))
                return m;
        return null;
    }
}
