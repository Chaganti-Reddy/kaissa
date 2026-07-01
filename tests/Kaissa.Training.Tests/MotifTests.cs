using Kaissa.Training;
using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class MotifTests
{
    // --- AttackBoard ---

    [Fact]
    public void Knight_in_the_centre_attacks_eight_squares()
    {
        var board = new AttackBoard("8/8/8/8/3N4/8/8/8 w - - 0 1"); // white knight d4
        var (f, r) = AttackBoard.Square("d4");
        Assert.Equal(8, board.AttacksFrom(f, r).Count());
    }

    [Fact]
    public void A_rook_is_blocked_by_the_first_piece_on_its_file()
    {
        var board = new AttackBoard("8/8/8/8/P7/8/8/R7 w - - 0 1"); // rook a1, own pawn a4
        var (f, r) = AttackBoard.Square("a1");
        var attacks = board.AttacksFrom(f, r).ToHashSet();
        Assert.Contains(AttackBoard.Square("a4"), attacks);  // blocker square is attacked
        Assert.DoesNotContain(AttackBoard.Square("a5"), attacks); // but nothing beyond it
    }

    [Fact]
    public void A_white_pawn_attacks_its_two_diagonals()
    {
        var board = new AttackBoard("8/8/8/8/4P3/8/8/8 w - - 0 1"); // pawn e4
        var (f, r) = AttackBoard.Square("e4");
        var attacks = board.AttacksFrom(f, r).ToHashSet();
        Assert.Equal(new HashSet<(int, int)> { AttackBoard.Square("d5"), AttackBoard.Square("f5") }, attacks);
    }

    // --- MotifClassifier ---

    [Fact]
    public void A_knight_check_that_also_hits_a_rook_is_a_fork()
    {
        // Black king a8, black rook e8; white knight b5 leaps to c7 attacking both.
        Assert.Equal(Motif.Fork, MotifClassifier.Classify("k3r3/8/8/1N6/8/8/8/7K w - - 0 1", "b5c7"));
    }

    [Fact]
    public void Capturing_an_undefended_piece_is_a_hanging_piece()
    {
        // Rook takes an undefended bishop on d5.
        Assert.Equal(Motif.HangingPiece, MotifClassifier.Classify("4k3/8/8/3b4/8/8/8/3RK3 w - - 0 1", "d1d5"));
    }

    [Fact]
    public void Delivering_mate_is_classified_as_checkmate()
    {
        Assert.Equal(Motif.Checkmate, MotifClassifier.Classify("6k1/5ppp/8/8/8/8/8/R6K w - - 0 1", "a1a8"));
    }

    [Fact]
    public void A_quiet_developing_move_is_unclassified()
    {
        Assert.Equal(Motif.Unclassified, MotifClassifier.Classify(ChessGameStart, "e2e4"));
    }

    // --- routing mistakes to patterns ---

    [Theory]
    [InlineData("k3r3/8/8/1N6/8/8/8/7K w - - 0 1", "b5c7", "tactic.fork")]
    [InlineData("4k3/8/8/3b4/8/8/8/3RK3 w - - 0 1", "d1d5", "tactic.hanging_piece")]
    [InlineData("6k1/5ppp/8/8/8/8/8/R6K w - - 0 1", "a1a8", "tactic.from_your_games")]
    public void A_missed_move_routes_to_the_pattern_for_its_motif(string fen, string best, string expectedPattern)
    {
        var assessment = new MoveAssessment(0, fen, "0000", best, 900, MoveQuality.Blunder);
        var scenario = Assert.Single(GamePractice.FromAssessments(new[] { assessment }));
        Assert.Equal(new PatternId(expectedPattern), scenario.Pattern);
        Assert.Equal(new[] { best }, scenario.Solutions);
    }

    private const string ChessGameStart = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
}
