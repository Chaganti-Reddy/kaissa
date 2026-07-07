using Kaissa.Chess.Engine;
using Kaissa.Chess.Rules;

namespace Kaissa.Training.Play;

/// <summary>
/// A single game between the player and an adaptive computer opponent. The player moves through
/// <see cref="TryPlayerMove"/>; the opponent replies through <see cref="EngineReplyAsync"/>. When
/// the game ends, <see cref="FinalizeRating"/> adjusts the player's rating by the result.
/// </summary>
public sealed class GameSession
{
    private readonly AdaptiveOpponent _opponent;
    private ChessGame _game;
    private readonly int _opponentElo;
    private readonly List<string> _moves = new();

    public GameSession(
        IChessEngine engine,
        Side playerSide,
        double playerRating,
        string? fen = null,
        AdaptiveOpponent? opponent = null)
    {
        _opponent = opponent ?? new AdaptiveOpponent(engine);
        StartFen = fen ?? ChessGame.StartFen;
        _game = ChessGame.FromFen(StartFen);
        PlayerSide = playerSide;
        PlayerRating = playerRating;
        _opponentElo = _opponent.TargetElo(playerRating);
    }

    public string StartFen { get; }
    public IReadOnlyList<string> MoveHistory => _moves;

    /// <summary>The move history in SAN (e4, Nf3, O-O, ...), rebuilt by replaying from the start.</summary>
    public IReadOnlyList<string> MoveHistorySan()
    {
        var game = ChessGame.FromFen(StartFen);
        var san = new List<string>(_moves.Count);
        foreach (var uci in _moves)
        {
            san.Add(game.SanForUci(uci) ?? uci);
            game.TryMakeMove(uci);
        }
        return san;
    }
    public Side PlayerSide { get; }
    public double PlayerRating { get; private set; }
    public int OpponentElo => _opponentElo;

    public string Fen => _game.Fen;
    public Side SideToMove => _game.SideToMove;
    public bool IsGameOver => _game.IsGameOver;
    public GameResult Result => _game.Result;
    public IReadOnlyList<string> LegalUciMoves() => _game.LegalUciMoves();

    /// <summary>Applies the player's move (SAN or UCI). False if it is not the player's turn or is illegal.</summary>
    public bool TryPlayerMove(string move)
    {
        if (_game.SideToMove != PlayerSide || _game.IsGameOver)
            return false;
        if (!_game.TryMakeMove(move))
            return false;
        _moves.Add(move);
        return true;
    }

    /// <summary>Plays the opponent's reply if it is their turn. Returns the move, or null if not applicable.</summary>
    public async Task<string?> EngineReplyAsync(CancellationToken cancellationToken = default)
    {
        if (_game.IsGameOver || _game.SideToMove == PlayerSide)
            return null;

        var move = await _opponent.ChooseMoveAsync(_game.Fen, PlayerRating, cancellationToken).ConfigureAwait(false);
        if (!_game.TryMakeMove(move))
            return null;
        _moves.Add(move);
        return move;
    }

    /// <summary>
    /// Takes back the last full move so it is the player's turn again: undoes the opponent's reply and
    /// the player's move (or just the player's move if the opponent has not replied). Rebuilds the
    /// position by replaying the remaining history. False if there is nothing to take back.
    /// </summary>
    public bool TryUndoFullMove()
    {
        if (_moves.Count == 0)
            return false;

        // If it is the player's turn, the last two plies are player+opponent; otherwise just the player's.
        int removeCount = _game.SideToMove == PlayerSide ? Math.Min(2, _moves.Count) : 1;
        _moves.RemoveRange(_moves.Count - removeCount, removeCount);

        _game = ChessGame.FromFen(StartFen);
        foreach (var m in _moves)
            _game.TryMakeMove(m);
        return true;
    }

    /// <summary>Once the game is over, updates the player's rating by the result against the opponent Elo.</summary>
    public void FinalizeRating()
    {
        if (!_game.IsGameOver)
            return;

        double score = _game.Result switch
        {
            GameResult.Draw => 0.5,
            GameResult.WhiteWins => PlayerSide == Side.White ? 1.0 : 0.0,
            GameResult.BlackWins => PlayerSide == Side.Black ? 1.0 : 0.0,
            _ => 0.5,
        };

        PlayerRating = RatingEstimator.Update(PlayerRating, _opponentElo, score);
    }
}
