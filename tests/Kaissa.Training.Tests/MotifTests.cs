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
    public void A_rook_holding_a_knight_against_the_king_is_a_pin()
    {
        // Re1 lines up against the knight on e5 with the black king behind on e8.
        Assert.Equal(Motif.Pin, MotifClassifier.Classify("4k3/8/8/4n3/8/8/8/R5K1 w - - 0 1", "a1e1"));
    }

    [Fact]
    public void A_rook_check_with_the_queen_behind_the_king_is_a_skewer()
    {
        // Ra8+ hits the king on d8 with the queen exposed behind on h8.
        Assert.Equal(Motif.Skewer, MotifClassifier.Classify("3k3q/8/8/8/8/8/8/R3K3 w - - 0 1", "a1a8"));
    }

    [Fact]
    public void Moving_a_knight_to_unveil_a_bishop_on_the_queen_is_a_discovered_attack()
    {
        // Bishop a1 is blocked by the knight on d4; Ne6 unveils the bishop's attack on the queen on h8.
        Assert.Equal(Motif.DiscoveredAttack, MotifClassifier.Classify("7q/8/7k/8/3N4/8/8/B3K3 w - - 0 1", "d4e6"));
    }

    [Fact]
    public void Delivering_mate_is_classified_as_checkmate()
    {
        // A queen mate with king support, on no particular geometry, is a plain checkmate.
        Assert.Equal(Motif.Checkmate, MotifClassifier.Classify("7k/Q7/5K2/8/8/8/8/8 w - - 0 1", "a7g7"));
    }

    [Theory]
    [InlineData("6k1/5ppp/8/8/8/8/8/R6K w - - 0 1", "a1a8", Motif.BackRankMate)]
    [InlineData("6rk/6pp/8/6N1/8/8/8/K7 w - - 0 1", "g5f7", Motif.SmotheredMate)]
    [InlineData("4k3/8/4N3/8/8/8/8/4R1K1 w - - 0 1", "e6c7", Motif.DoubleCheck)]
    public void Forcing_motifs_are_classified(string fen, string move, Motif expected)
    {
        Assert.Equal(expected, MotifClassifier.Classify(fen, move));
    }

    [Fact]
    public void A_quiet_developing_move_is_unclassified()
    {
        Assert.Equal(Motif.Unclassified, MotifClassifier.Classify(ChessGameStart, "e2e4"));
    }

    // --- routing mistakes to patterns ---

    [Theory]
    [InlineData("k3r3/8/8/1N6/8/8/8/7K w - - 0 1", "b5c7", "tactic.fork")]
    [InlineData("4k3/8/8/4n3/8/8/8/R5K1 w - - 0 1", "a1e1", "tactic.pin")]
    [InlineData("3k3q/8/8/8/8/8/8/R3K3 w - - 0 1", "a1a8", "tactic.skewer")]
    [InlineData("7q/8/7k/8/3N4/8/8/B3K3 w - - 0 1", "d4e6", "tactic.discovered_attack")]
    [InlineData("4k3/8/8/3b4/8/8/8/3RK3 w - - 0 1", "d1d5", "tactic.hanging_piece")]
    [InlineData("6k1/5ppp/8/8/8/8/8/R6K w - - 0 1", "a1a8", "checkmate.back_rank")]
    [InlineData("6rk/6pp/8/6N1/8/8/8/K7 w - - 0 1", "g5f7", "checkmate.smothered")]
    [InlineData("4k3/8/4N3/8/8/8/8/4R1K1 w - - 0 1", "e6c7", "tactic.double_check")]
    [InlineData("7k/Q7/5K2/8/8/8/8/8 w - - 0 1", "a7g7", "tactic.from_your_games")]
    public void A_missed_move_routes_to_the_pattern_for_its_motif(string fen, string best, string expectedPattern)
    {
        var assessment = new MoveAssessment(0, Kaissa.Chess.Rules.Side.White, fen, "0000", "0000", best, best, 900, MoveQuality.Blunder, 0, -900);
        var scenario = Assert.Single(GamePractice.FromAssessments(new[] { assessment }));
        Assert.Equal(new PatternId(expectedPattern), scenario.Pattern);
        Assert.Equal(new[] { best }, scenario.Solutions);
    }

    private const string ChessGameStart = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
}
