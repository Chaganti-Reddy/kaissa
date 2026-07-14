using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public class MoveClassifierJudgementTests
{
    private static MoveJudgement J(int loss, int bestCp = 0, int playedCp = 0, int gap = 0,
        bool book = false, bool sac = false) =>
        new(loss, bestCp, playedCp, gap, book, sac);

    [Fact]
    public void Book_move_wins_over_everything()
    {
        Assert.Equal(MoveQuality.Book, MoveClassifier.Classify(J(0, book: true)));
        Assert.Equal(MoveQuality.Book, MoveClassifier.Classify(J(500, book: true)));
    }

    [Fact]
    public void Brilliant_needs_a_sac_that_is_not_losing_and_was_not_already_winning()
    {
        // best move, sacrifice, still fine after, and the alternative was NOT already winning
        Assert.Equal(MoveQuality.Brilliant,
            MoveClassifier.Classify(J(0, bestCp: 120, playedCp: 120, gap: 40, sac: true)));
    }

    [Fact]
    public void Not_brilliant_when_already_winning_without_the_sac()
    {
        // alternative eval = best(500) - gap(40) = 460 >= 300 -> already winning -> just Best
        Assert.Equal(MoveQuality.Best,
            MoveClassifier.Classify(J(0, bestCp: 500, playedCp: 500, gap: 40, sac: true)));
    }

    [Fact]
    public void Not_brilliant_when_losing_after_the_sac()
    {
        Assert.Equal(MoveQuality.Best,
            MoveClassifier.Classify(J(0, bestCp: 100, playedCp: -200, gap: 40, sac: true)));
    }

    [Fact]
    public void Great_is_the_only_good_move()
    {
        Assert.Equal(MoveQuality.Great,
            MoveClassifier.Classify(J(0, bestCp: 60, playedCp: 60, gap: 220)));
    }

    [Fact]
    public void Miss_is_a_squandered_winning_chance()
    {
        // best was winning (>=200), lost a lot (>=100), but not losing (played >= -100)
        Assert.Equal(MoveQuality.Miss,
            MoveClassifier.Classify(J(150, bestCp: 400, playedCp: 250)));
    }

    [Fact]
    public void Ordinary_tiers_still_hold()
    {
        Assert.Equal(MoveQuality.Best, MoveClassifier.Classify(J(5)));
        Assert.Equal(MoveQuality.Excellent, MoveClassifier.Classify(J(20)));
        Assert.Equal(MoveQuality.Good, MoveClassifier.Classify(J(45)));
        Assert.Equal(MoveQuality.Inaccuracy, MoveClassifier.Classify(J(80)));
        Assert.Equal(MoveQuality.Mistake, MoveClassifier.Classify(J(150)));   // best not winning -> not a Miss
        Assert.Equal(MoveQuality.Blunder, MoveClassifier.Classify(J(500)));
    }
}
