using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public class AccuracyTests
{
    private static MoveAssessment Move(int loss, int bestEvalCp = 0) =>
        new(0, "8/8/8/8/8/8/8/8 w - - 0 1", "e2e4", "e2e4", loss, MoveQuality.Best, bestEvalCp);

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

    [Fact]
    public void The_same_centipawn_loss_hurts_less_when_the_position_is_already_lopsided()
    {
        // Giving up 100cp from a dead-equal position drops more win% than from a winning position.
        double fromEqual = AccuracyModel.GameAccuracy(new[] { Move(100, bestEvalCp: 0) });
        double fromWinning = AccuracyModel.GameAccuracy(new[] { Move(100, bestEvalCp: 800) });
        Assert.True(fromWinning > fromEqual);
    }
}
