using Kaissa.Chess.Rules;
using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public class AccuracyTests
{
    private static MoveAssessment Move(int loss, int bestEvalCp = 0) =>
        new(0, Side.White, "8/8/8/8/8/8/8/8 w - - 0 1", "e2e4", "e4", "e2e4", "e4", loss, MoveQuality.Best, bestEvalCp, bestEvalCp - loss);

    [Fact]
    public void Perfect_play_is_100_percent()
    {
        var moves = new[] { Move(0), Move(0), Move(0) };
        Assert.Equal(100.0, AccuracyModel.GameAccuracy(moves), 1);
    }

    [Fact]
    public void No_moves_is_100_percent()
    {
        Assert.Equal(100.0, AccuracyModel.GameAccuracy(System.Array.Empty<MoveAssessment>()), 1);
    }

    [Fact]
    public void A_large_blunder_lowers_accuracy_well_below_a_small_slip()
    {
        double blunder = AccuracyModel.GameAccuracy(new[] { Move(900) });
        double slip = AccuracyModel.GameAccuracy(new[] { Move(30) });
        Assert.True(blunder < slip);
        Assert.True(slip > 85.0);   // a tiny slip is still near-perfect
        Assert.True(blunder < 60.0); // a big blunder is clearly penalised
    }

    [Fact]
    public void Accuracy_stays_in_range()
    {
        foreach (var loss in new[] { 0, 10, 50, 100, 300, 1000, 5000 })
        {
            double acc = AccuracyModel.GameAccuracy(new[] { Move(loss) });
            Assert.InRange(acc, 0.0, 100.0);
        }
    }

    private const string Start = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private const string KingAndPawn = "8/8/8/4k3/8/8/4P3/4K3 w - - 0 1";

    private static MoveAssessment MoveAt(string fen, int ply, int loss = 0) =>
        new(ply, Side.White, fen, "e2e4", "e4", "e2e4", "e4", loss, MoveQuality.Best, 0, -loss);

    [Fact]
    public void Phases_are_classified_by_material_and_move_number()
    {
        Assert.Equal(GamePhase.Opening, GamePhaseClassifier.Classify(Start, 0));
        Assert.Equal(GamePhase.Middlegame, GamePhaseClassifier.Classify(Start, 30));
        Assert.Equal(GamePhase.Endgame, GamePhaseClassifier.Classify(KingAndPawn, 60));
    }

    [Fact]
    public void Accuracy_by_phase_groups_moves_and_leaves_empty_phases_null()
    {
        var moves = new[] { MoveAt(Start, 2), MoveAt(KingAndPawn, 60) };
        var byPhase = AccuracyModel.ByPhase(moves);
        Assert.NotNull(byPhase.Opening);
        Assert.NotNull(byPhase.Endgame);
        Assert.Null(byPhase.Middlegame); // no move fell in the middlegame
    }

    [Fact]
    public void The_same_centipawn_loss_hurts_less_when_the_position_is_already_lopsided()
    {
        // Giving up 100cp from a dead-equal position drops more win% than from a winning position.
        double fromEqual = AccuracyModel.GameAccuracy(new[] { Move(100, bestEvalCp: 0) });
        double fromWinning = AccuracyModel.GameAccuracy(new[] { Move(100, bestEvalCp: 800) });
        Assert.True(fromWinning > fromEqual);
    }
}
