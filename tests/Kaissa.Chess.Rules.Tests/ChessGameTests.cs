using Kaissa.Chess.Rules;
using Xunit;

namespace Kaissa.Chess.Rules.Tests;

public sealed class ChessGameTests
{
    [Fact]
    public void Start_position_has_twenty_legal_moves_and_white_to_move()
    {
        var game = ChessGame.Start();
        Assert.Equal(20, game.LegalMoves().Count);
        Assert.Equal(20, game.LegalUciMoves().Count);
        Assert.Equal(Side.White, game.SideToMove);
        Assert.False(game.IsGameOver);
    }

    [Fact]
    public void Fen_round_trips()
    {
        Assert.Equal(ChessGame.StartFen, ChessGame.Start().Fen);
    }

    [Fact]
    public void Move_can_be_made_by_san_and_by_uci()
    {
        var bySan = ChessGame.Start();
        Assert.True(bySan.TryMakeMove("e4"));
        Assert.Equal(Side.Black, bySan.SideToMove);

        var byUci = ChessGame.Start();
        Assert.True(byUci.TryMakeMove("e2e4"));
        Assert.Equal(Side.Black, byUci.SideToMove);

        // Both routes reach the same position.
        Assert.Equal(bySan.Fen, byUci.Fen);
    }

    [Fact]
    public void Illegal_move_is_rejected()
    {
        var game = ChessGame.Start();
        Assert.False(game.TryMakeMove("e2e5")); // pawn cannot jump three squares
        Assert.False(game.TryMakeMove("Qz9"));  // nonsense
        Assert.Equal(Side.White, game.SideToMove); // position unchanged
    }

    [Fact]
    public void Uci_legal_moves_are_well_formed_and_include_double_pawn_push()
    {
        var uci = ChessGame.Start().LegalUciMoves();
        Assert.Contains("e2e4", uci);
        Assert.All(uci, m => Assert.InRange(m.Length, 4, 5));
    }

    [Fact]
    public void Fools_mate_is_checkmate_won_by_black()
    {
        Assert.True(ChessGame.TryFromPgn("1. f3 e5 2. g4 Qh4#", out var game));
        Assert.NotNull(game);
        Assert.True(game!.IsGameOver);
        Assert.True(game.IsCheckmate);
        Assert.Equal(GameResult.BlackWins, game.Result);
    }

    [Fact]
    public void Stalemate_is_detected_as_a_draw()
    {
        // Black to move, not in check, no legal move.
        var game = ChessGame.FromFen("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
        Assert.True(game.IsStalemate);
        Assert.True(game.IsDraw);
        Assert.Equal(GameResult.Draw, game.Result);
    }

    [Fact]
    public void King_versus_king_is_an_insufficient_material_draw()
    {
        var game = ChessGame.FromFen("8/8/8/4k3/8/4K3/8/8 w - - 0 1");
        Assert.True(game.IsGameOver);
        Assert.True(game.IsDraw);
    }

    [Fact]
    public void Promotion_move_is_expressed_in_uci_with_a_piece_suffix()
    {
        // White pawn on e7, ready to promote.
        var game = ChessGame.FromFen("8/4P1k1/8/8/8/8/6K1/8 w - - 0 1");
        var uci = game.LegalUciMoves();
        Assert.Contains("e7e8q", uci);
        Assert.True(game.TryMakeMove("e7e8q"));
    }
}
