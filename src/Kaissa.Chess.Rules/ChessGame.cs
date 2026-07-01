using Chess;

namespace Kaissa.Chess.Rules;

/// <summary>Side to move.</summary>
public enum Side
{
    White,
    Black,
}

/// <summary>Outcome of a game.</summary>
public enum GameResult
{
    Ongoing,
    WhiteWins,
    BlackWins,
    Draw,
}

/// <summary>
/// A chess position with full rules: legal move generation, move application (SAN or UCI),
/// FEN/PGN, and check/mate/draw detection. This wraps the Gera.Chess library so the rest of the
/// codebase never depends on it directly and it can be replaced without touching callers.
/// </summary>
public sealed class ChessGame
{
    /// <summary>Standard starting position in FEN.</summary>
    public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private readonly ChessBoard _board;

    private ChessGame(ChessBoard board) => _board = board;

    public static ChessGame Start() => FromFen(StartFen);

    public static ChessGame FromFen(string fen) =>
        new(ChessBoard.LoadFromFen(fen, AutoEndgameRules.All));

    public static bool TryFromPgn(string pgn, out ChessGame? game)
    {
        if (ChessBoard.TryLoadFromPgn(pgn, out var board, AutoEndgameRules.All))
        {
            game = new ChessGame(board);
            return true;
        }

        game = null;
        return false;
    }

    public string Fen => _board.ToFen();

    public string Pgn => _board.ToPgn();

    public Side SideToMove => _board.Turn == PieceColor.White ? Side.White : Side.Black;

    /// <summary>Legal moves in Standard Algebraic Notation (e.g. "Nf3", "exd5", "O-O").</summary>
    public IReadOnlyList<string> LegalMoves() =>
        _board.Moves().Select(m => m.San ?? ToUci(m)).ToList();

    /// <summary>Legal moves in UCI long algebraic notation (e.g. "e2e4", "e7e8q").</summary>
    public IReadOnlyList<string> LegalUciMoves() =>
        _board.Moves().Select(ToUci).ToList();

    /// <summary>
    /// Applies a move given as SAN or UCI. Returns false if the move is illegal in this position.
    /// </summary>
    public bool TryMakeMove(string move)
    {
        foreach (var candidate in _board.Moves())
        {
            if (string.Equals(candidate.San, move, StringComparison.Ordinal) ||
                string.Equals(ToUci(candidate), move, StringComparison.OrdinalIgnoreCase))
            {
                return _board.Move(candidate);
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the UCI form of a legal move given as SAN or UCI, or null if it is not legal here.
    /// Lets callers compare a player's move to a stored solution regardless of notation.
    /// </summary>
    public string? ResolveToUci(string move)
    {
        foreach (var candidate in _board.Moves())
        {
            if (string.Equals(candidate.San, move, StringComparison.Ordinal) ||
                string.Equals(ToUci(candidate), move, StringComparison.OrdinalIgnoreCase))
            {
                return ToUci(candidate);
            }
        }

        return null;
    }

    public bool IsCheck => _board.WhiteKingChecked || _board.BlackKingChecked;

    public bool IsGameOver => _board.IsEndGame;

    public bool IsCheckmate => _board.EndGame?.EndgameType == EndgameType.Checkmate;

    public bool IsStalemate => _board.EndGame?.EndgameType == EndgameType.Stalemate;

    public bool IsDraw => _board.IsEndGame && _board.EndGame!.WonSide is null;

    public GameResult Result
    {
        get
        {
            if (!_board.IsEndGame)
                return GameResult.Ongoing;

            var wonSide = _board.EndGame!.WonSide;
            if (wonSide is null)
                return GameResult.Draw;

            return wonSide == PieceColor.White ? GameResult.WhiteWins : GameResult.BlackWins;
        }
    }

    private static string ToUci(Move move)
    {
        var uci = move.OriginalPosition.ToString() + move.NewPosition.ToString();

        // Append the promotion piece (e.g. "e7e8q"), reading it from the SAN the library produced.
        if (move.IsPromotion && move.San is { } san)
        {
            var eq = san.IndexOf('=');
            if (eq >= 0 && eq + 1 < san.Length)
                uci += char.ToLowerInvariant(san[eq + 1]);
        }

        return uci;
    }
}
