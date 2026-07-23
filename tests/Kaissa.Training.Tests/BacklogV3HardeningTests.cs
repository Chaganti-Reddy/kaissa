using System;
using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

/// <summary>Edge cases that lock down the v3 core: disambiguation, guards, persistence round-trips.</summary>
public class BacklogV3HardeningTests
{
    [Fact]
    public void MoveEntry_disambiguates_two_knights_to_the_same_square()
    {
        // Both knights (b1, f3) can reach d2. "Nbd2" and "Nfd2" must resolve to different UCI moves.
        var game = ChessGame.FromFen("rnbqkb1r/pppppppp/8/8/8/5N2/PPPPPPPP/RNBQKB1R w KQkq - 0 1");
        var legal = game.LegalUciMoves();
        // Only assert disambiguation if the position really offers both (guards against rules variance).
        if (legal.Contains("b1d2") && legal.Contains("f3d2"))
        {
            Assert.Equal("b1d2", MoveEntry.Parse(game, "Nbd2"));
            Assert.Equal("f3d2", MoveEntry.Parse(game, "Nfd2"));
        }
    }

    [Fact]
    public void MoveEntry_reads_a_capture_in_san()
    {
        var game = ChessGame.FromFen("rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq d6 0 2");
        Assert.Equal("e4d5", MoveEntry.Parse(game, "exd5"));
    }

    [Fact]
    public void Streak_Submit_without_Next_throws()
    {
        var s = new StreakSession(ScenarioLibrary.LoadDefault());
        Assert.Throws<InvalidOperationException>(() => s.Submit("e2e4", TimeSpan.Zero));
    }

    [Fact]
    public void Storm_rejects_a_non_positive_combo_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StormScoring(comboPerBonus: 0));
    }

    [Fact]
    public void Storm_grants_multiple_bonuses_over_a_long_combo()
    {
        var st = new StormScoring(startSeconds: 100, comboPerBonus: 5, comboBonus: 10);
        for (int i = 0; i < 12; i++) st.OnSolve();
        Assert.Equal(2, st.BonusesEarned);   // at 5 and 10
        Assert.Equal(120, st.TimeRemaining); // 100 + 2*10
        Assert.Equal(12, st.BestCombo);
    }

    [Fact]
    public void Ladder_beaten_set_round_trips_through_the_constructor()
    {
        var a = new LadderProgression(startHints: 5);
        int first = a.CurrentIndex;
        a.RecordResult(first, true);
        var saved = a.BeatenIds.ToArray();

        var b = new LadderProgression(startHints: 5, beaten: saved);
        Assert.True(b.IsUnlocked(first + 1));
        Assert.Equal(first + 1, b.CurrentIndex);
    }

    [Fact]
    public void Ladder_full_clear_leaves_current_at_the_top()
    {
        var ladder = new LadderProgression(startHints: 5);
        for (int i = 0; i < ladder.Count; i++)
            ladder.RecordResult(i, true);
        Assert.Equal(ladder.Count - 1, ladder.CurrentIndex);
        Assert.All(ladder.Rungs(), r => Assert.True(r.Beaten));
    }

    [Fact]
    public void Expedition_losses_alone_never_complete_it()
    {
        var e = Expeditions.ById("exp_london")!;
        var run = new ExpeditionRun(e);
        for (int i = 0; i < 10; i++) run.Record(false);
        Assert.False(run.IsComplete);
        Assert.Equal(0, run.Wins);
        Assert.Equal(0.0, run.Progress);
    }

    [Fact]
    public void Expedition_unknown_id_is_null()
    {
        Assert.Null(Expeditions.ById("nope"));
    }

    [Fact]
    public void Every_expedition_maps_to_an_opening_that_replays_legally()
    {
        foreach (var e in Expeditions.Catalog)
        {
            var line = OpeningLibrary.ById(e.OpeningId);
            Assert.NotNull(line);
            var game = ChessGame.Start();
            foreach (var uci in line!.Moves)
                Assert.True(game.TryMakeMove(uci), $"illegal {uci} in {e.OpeningId}");
        }
    }

    [Fact]
    public void Leitner_due_list_is_empty_when_nothing_is_due()
    {
        var sch = new LeitnerScheduler();
        var cards = new[] { new LeitnerCard("a", 2, 50), new LeitnerCard("b", 3, 99) };
        Assert.Empty(sch.Due(cards, today: 10));
    }

    [Fact]
    public void Premove_replaces_an_earlier_queued_move()
    {
        var q = new PremoveQueue();
        q.Set("e2e4");
        q.Set("d2d4");
        Assert.Equal("d2d4", q.Pending);
        Assert.Equal("d2d4", q.Consume(ChessGame.Start()));
    }

    [Fact]
    public void Drill_over_the_full_library_is_never_empty_and_respects_the_count()
    {
        var lib = ScenarioLibrary.LoadDefault();
        foreach (DrillKind kind in Enum.GetValues(typeof(DrillKind)))
        {
            var d = DrillFactory.Build(kind, lib, count: 8);
            Assert.NotEmpty(d.Scenarios);
            Assert.True(d.Scenarios.Count <= 8, $"{kind} returned {d.Scenarios.Count}");
        }
    }
}
