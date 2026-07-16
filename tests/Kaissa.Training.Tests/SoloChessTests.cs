using System.Linq;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class SoloChessTests
{
    [Theory]
    [InlineData(3, false, 11)]
    [InlineData(4, false, 22)]
    [InlineData(5, true, 33)]
    [InlineData(6, true, 44)]
    public void Generated_boards_are_solvable_with_the_right_piece_count(int pieces, bool withKing, int seed)
    {
        var placement = SoloChess.Generate(pieces, withKing, seed);
        Assert.NotNull(placement);
        var solo = new SoloChess(placement!);
        Assert.Equal(pieces, solo.PieceCount);
        Assert.True(solo.IsSolvable());
    }

    [Fact]
    public void A_generated_board_can_actually_be_played_down_to_one_piece()
    {
        var placement = SoloChess.Generate(5, withKing: false, seed: 7);
        Assert.NotNull(placement);
        var solo = new SoloChess(placement!);

        // Greedily follow any solving line (depth-first) to the end.
        int guard = 50;
        while (!solo.Solved && guard-- > 0)
        {
            SoloMove? chosen = solo.LegalMoves().FirstOrDefault(m =>
            {
                var probe = solo.Clone();
                probe.TryApply(m);
                return probe.IsSolvable();
            });
            Assert.NotNull(chosen); // a solvable board always has a move that keeps it solvable
            Assert.True(solo.TryApply(chosen!));
        }
        Assert.True(solo.Solved);
    }

    [Fact]
    public void Every_move_must_capture_and_a_king_is_never_a_target()
    {
        // White rook a1, knight b3, king e1. The rook/knight can capture; nothing may capture the king.
        var solo = new SoloChess("8/8/8/8/8/1N6/8/R3K3");
        var moves = solo.LegalMoves();
        Assert.All(moves, m => Assert.NotEqual('\0', solo.PieceAt(m.To)));         // always a capture
        Assert.DoesNotContain(moves, m => char.ToUpperInvariant(solo.PieceAt(m.To)) == 'K'); // king safe
    }

    [Fact]
    public void A_piece_that_captured_twice_cannot_move_again()
    {
        // Rook can take two pieces in a row along the file; after two it is stuck.
        var solo = new SoloChess("8/8/8/8/R7/R7/R7/R7"); // four rooks stacked on the a-file
        Assert.True(solo.TryApply(new SoloMove("a1", "a2")));
        Assert.True(solo.TryApply(new SoloMove("a2", "a3")));
        // The mover on a3 has now captured twice - it may no longer be a From square.
        Assert.DoesNotContain(solo.LegalMoves(), m => m.From == "a3");
    }

    [Fact]
    public void Illegal_moves_are_rejected()
    {
        var solo = new SoloChess("8/8/8/8/8/1N6/8/R3K3");
        Assert.False(solo.TryApply(new SoloMove("a1", "a5"))); // a5 empty - not a capture
        Assert.False(solo.TryApply(new SoloMove("a1", "h8"))); // nothing there
    }
}
