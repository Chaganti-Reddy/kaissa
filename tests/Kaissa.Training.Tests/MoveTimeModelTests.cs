using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public class MoveTimeModelTests
{
    private static MoveTimeContext Ctx(bool book = false, bool recap = false, bool forced = false,
        int legal = 20, double? remaining = 180, double inc = 0, int tempo = 1) =>
        new(book, recap, forced, legal, remaining, inc, tempo);

    [Fact]
    public void Book_and_forced_moves_are_quick()
    {
        double book = MoveTimeModel.ThinkSeconds(Ctx(book: true), 0.5);
        double forced = MoveTimeModel.ThinkSeconds(Ctx(forced: true, legal: 1), 0.5);
        double complex = MoveTimeModel.ThinkSeconds(Ctx(legal: 38), 0.5);
        Assert.True(book < 1.0);
        Assert.True(forced < 1.5);
        Assert.True(complex > book);
        Assert.True(complex > forced);
    }

    [Fact]
    public void Complex_positions_take_longer_than_quiet_ones()
    {
        double quiet = MoveTimeModel.ThinkSeconds(Ctx(legal: 12), 0.5);
        double complex = MoveTimeModel.ThinkSeconds(Ctx(legal: 40), 0.5);
        Assert.True(complex > quiet);
    }

    [Fact]
    public void Low_on_time_moves_fast()
    {
        double normal = MoveTimeModel.ThinkSeconds(Ctx(legal: 30, remaining: 180), 0.5);
        double panic = MoveTimeModel.ThinkSeconds(Ctx(legal: 30, remaining: 6), 0.5);
        Assert.True(panic < normal);
        Assert.True(panic <= 1.0);
    }

    [Fact]
    public void Never_burns_more_than_a_fifth_of_the_clock()
    {
        double t = MoveTimeModel.ThinkSeconds(Ctx(legal: 40, remaining: 20, tempo: 2), 1.0);
        Assert.True(t <= 20 * 0.20 + 0.001);
    }

    [Fact]
    public void Untimed_games_still_produce_a_sane_delay()
    {
        double t = MoveTimeModel.ThinkSeconds(Ctx(legal: 25, remaining: null), 0.5);
        Assert.InRange(t, 0.3, 6.0);
    }
}
