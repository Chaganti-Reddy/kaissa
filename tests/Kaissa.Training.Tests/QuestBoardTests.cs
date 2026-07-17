using System.Linq;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class QuestBoardTests
{
    private static QuestSnapshot Snap(int puzzles = 0, int wins = 0, int streak = 0, int days = 0,
        int bots = 0, int mem = 0, int vis = 0, int solo = 0) =>
        new(puzzles, wins, streak, days, bots, mem, vis, solo);

    [Fact]
    public void Quests_report_progress_toward_their_target()
    {
        var q = QuestBoard.For(Snap(puzzles: 25)).First(x => x.Title == "Solver");
        Assert.Equal(25, q.Current);
        Assert.Equal(50, q.Target);
        Assert.False(q.Done);
        Assert.Equal(0.5, q.Fraction, 3);
    }

    [Fact]
    public void A_target_met_marks_the_quest_done()
    {
        var q = QuestBoard.For(Snap(wins: 12)).First(x => x.Title == "Competitor");
        Assert.True(q.Done);
        Assert.Equal(1.0, q.Fraction, 3);
    }

    [Fact]
    public void Fresh_player_is_the_lowest_rank()
    {
        var (name, index) = QuestBoard.Rank(Snap());
        Assert.Equal("Pawn", name);
        Assert.Equal(0, index);
        Assert.Equal(0, QuestBoard.CompletedCount(Snap()));
    }

    [Fact]
    public void Rank_rises_as_quests_complete()
    {
        // Complete several quests at once.
        var strong = Snap(puzzles: 100, wins: 20, streak: 20, days: 10, bots: 6, mem: 12, vis: 5, solo: 9);
        Assert.Equal(8, QuestBoard.CompletedCount(strong)); // all eight done
        var (name, index) = QuestBoard.Rank(strong);
        Assert.True(index > 0);
        Assert.Equal("King", name); // 8 done / 2 = 4, clamped into range -> top rank
    }
}
