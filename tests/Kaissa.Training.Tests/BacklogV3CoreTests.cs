using System;
using System.Collections.Generic;
using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

/// <summary>
/// Covers the pure-core features added in the v3 backlog batch: puzzle Streak, Storm combo scoring,
/// keyboard move entry, premoves, the Leitner scheduler, the named drill generators, the bot ladder,
/// and opening expeditions. All deterministic, no engine, no Unity.
/// </summary>
public class BacklogV3CoreTests
{
    // A tiny, controlled library so the drill filters can be asserted exactly.
    private static ScenarioLibrary TinyLibrary()
    {
        var patterns = new[]
        {
            new Pattern(new PatternId("checkmate.back_rank"), "Back-rank mate", ""),
            new Pattern(new PatternId("tactic.fork"), "Fork", ""),
            new Pattern(new PatternId("tactic.hanging_piece"), "Hanging piece", ""),
            new Pattern(new PatternId("tactic.pin"), "Pin", ""),
        };
        var scenarios = new[]
        {
            // #1 mate, fullmove 1, white up a rook
            new Scenario("mate1", new PatternId("checkmate.back_rank"), "6k1/5ppp/8/8/8/8/8/R6K w - - 0 1",
                new[] { "a1a8" }, "Mate in one.", 1200, Themes: new[] { "mate", "mateIn1" }),
            // #2 advantage/crushing, fullmove 10, white down material but wins the queen
            new Scenario("adv1", new PatternId("tactic.fork"), "r3k2r/ppp2ppp/2n5/3q4/3P4/2N5/PPP2PPP/R3K2R w KQkq - 0 10",
                new[] { "c3d5" }, "Win material.", 1500, Themes: new[] { "advantage", "crushing" }),
            // #3 defender: black to move, down a queen, fullmove 12
            new Scenario("def1", new PatternId("tactic.hanging_piece"), "r3k2r/ppp2ppp/8/8/8/8/PPP2PPP/R2QK2R b KQkq - 0 12",
                new[] { "e8g8" }, "Hold on.", 1400, Themes: new[] { "endgame" }),
            // #4 opening, fullmove 1, material even
            new Scenario("open1", new PatternId("tactic.pin"), "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
                new[] { "e7e5" }, "Reply.", 900, Themes: new[] { "opening", "short" }),
        };
        return new ScenarioLibrary(patterns, scenarios);
    }

    // -- Streak -------------------------------------------------------------

    [Fact]
    public void Streak_solve_raises_score_and_first_miss_ends_it()
    {
        var s = new StreakSession(TinyLibrary(), startRating: 1400);
        var p = s.Next();
        Assert.NotNull(p);
        var r = s.Submit(p!.Solutions[0], TimeSpan.FromSeconds(3));
        Assert.True(r.Correct);
        Assert.Equal(1, s.Score);
        Assert.False(s.IsOver);

        var p2 = s.Next();
        var miss = s.Submit("a1a1", TimeSpan.FromSeconds(3)); // not a solution -> graded wrong
        Assert.False(miss.Correct);
        Assert.True(s.IsOver);
        Assert.Null(s.Next());
    }

    [Fact]
    public void Streak_allows_exactly_one_skip()
    {
        var s = new StreakSession(TinyLibrary());
        s.Next();
        Assert.True(s.Skip());
        Assert.True(s.SkipUsed);
        s.Next();
        Assert.False(s.Skip()); // second skip refused
    }

    // -- Storm --------------------------------------------------------------

    [Fact]
    public void Storm_combo_grants_bonus_at_the_milestone()
    {
        var st = new StormScoring(startSeconds: 100, wrongPenalty: 10, comboPerBonus: 5, comboBonus: 10);
        for (int i = 0; i < 4; i++) st.OnSolve();
        Assert.Equal(4, st.Combo);
        Assert.Equal(100, st.TimeRemaining);
        Assert.Equal(1, st.ToNextBonus);

        st.OnSolve(); // fifth -> bonus
        Assert.Equal(5, st.Combo);
        Assert.Equal(110, st.TimeRemaining);
        Assert.Equal(1, st.BonusesEarned);
        Assert.Equal(5, st.BestCombo);
    }

    [Fact]
    public void Storm_miss_resets_combo_and_costs_time_and_clock_can_run_out()
    {
        var st = new StormScoring(startSeconds: 15, wrongPenalty: 10);
        st.OnSolve();
        st.OnMiss();
        Assert.Equal(0, st.Combo);
        Assert.Equal(1, st.BestCombo);
        Assert.Equal(5, st.TimeRemaining);

        st.Tick(10); // past zero
        Assert.True(st.IsOver);
        int solvedBefore = st.Solved;
        st.OnSolve(); // ignored once over
        Assert.Equal(solvedBefore, st.Solved);
    }

    // -- Keyboard move entry ------------------------------------------------

    [Theory]
    [InlineData("e4", "e2e4")]
    [InlineData("Nf3", "g1f3")]
    [InlineData("nf3", "g1f3")]   // forgiving lower-case piece letter
    [InlineData("e2e4", "e2e4")]  // raw UCI
    [InlineData("e2-e4", "e2e4")] // long algebraic
    [InlineData("e4+", "e2e4")]   // stray check mark
    public void MoveEntry_parses_the_common_forms(string typed, string expected)
    {
        var game = ChessGame.Start();
        Assert.Equal(expected, MoveEntry.Parse(game, typed));
    }

    [Theory]
    [InlineData("e5")]   // black's move, not legal for white
    [InlineData("Qd5")]  // no such legal move
    [InlineData("")]
    [InlineData("   ")]
    public void MoveEntry_rejects_illegal_or_empty(string typed)
    {
        Assert.Null(MoveEntry.Parse(ChessGame.Start(), typed));
    }

    [Fact]
    public void MoveEntry_handles_castling_written_with_zeros()
    {
        var game = ChessGame.FromFen("r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/2N2N2/PPPP1PPP/R1BQK2R w KQkq - 0 1");
        Assert.Equal("e1g1", MoveEntry.Parse(game, "O-O"));
        Assert.Equal("e1g1", MoveEntry.Parse(game, "0-0"));
    }

    [Fact]
    public void MoveEntry_handles_promotion()
    {
        var game = ChessGame.FromFen("8/P7/8/8/8/8/8/k6K w - - 0 1");
        Assert.Equal("a7a8q", MoveEntry.Parse(game, "a8=Q"));
        Assert.Equal("a7a8q", MoveEntry.Parse(game, "a7a8q"));
    }

    // -- Premove ------------------------------------------------------------

    [Fact]
    public void Premove_applies_when_still_legal_else_discards()
    {
        var q = new PremoveQueue();
        Assert.True(q.Set("e2e4"));
        Assert.True(q.HasPremove);
        Assert.Equal("e2e4", q.Consume(ChessGame.Start()));
        Assert.False(q.HasPremove); // consumed

        q.Set("e7e5"); // not legal for white to move
        Assert.Null(q.Consume(ChessGame.Start()));

        Assert.False(q.Set(null));
        Assert.False(q.Set("xx"));
    }

    // -- Leitner ------------------------------------------------------------

    [Fact]
    public void Leitner_promotes_on_correct_and_resets_on_wrong()
    {
        var sch = new LeitnerScheduler(); // 1,2,4,8,16
        var card = LeitnerCard.New("c1");
        Assert.Equal(1, card.Box);

        card = sch.Review(card, correct: true, today: 0);
        Assert.Equal(2, card.Box);
        Assert.Equal(2, card.DueDay); // today 0 + interval 2

        card = sch.Review(card, correct: true, today: 2);
        Assert.Equal(3, card.Box);
        Assert.Equal(6, card.DueDay); // 2 + 4

        card = sch.Review(card, correct: false, today: 6);
        Assert.Equal(1, card.Box);
        Assert.Equal(7, card.DueDay); // 6 + 1
    }

    [Fact]
    public void Leitner_caps_at_the_top_box_and_lists_due_hardest_first()
    {
        var sch = new LeitnerScheduler(1, 2, 4);
        var card = LeitnerCard.New("c1");
        for (int i = 0; i < 10; i++) card = sch.Review(card, true, today: 0);
        Assert.Equal(3, card.Box); // capped at box count

        var cards = new[]
        {
            new LeitnerCard("a", 3, 5),
            new LeitnerCard("b", 1, 0),
            new LeitnerCard("c", 2, 20), // not due
        };
        var due = sch.Due(cards, today: 10);
        Assert.Equal(new[] { "b", "a" }, due.Select(c => c.Id).ToArray());
    }

    // -- Drills -------------------------------------------------------------

    [Fact]
    public void Drill_checkmate_selects_only_mates()
    {
        var d = DrillFactory.Build(DrillKind.CheckmatePatterns, TinyLibrary());
        Assert.Equal(DrillKind.CheckmatePatterns, d.Kind);
        Assert.Equal("Checkmate Patterns", d.Title);
        Assert.NotEmpty(d.Scenarios);
        Assert.All(d.Scenarios, s =>
            Assert.True(s.Pattern.Value.StartsWith("checkmate.") || s.ThemeTags.Contains("mate")));
    }

    [Fact]
    public void Drill_advantage_selects_by_theme_and_defender_by_material()
    {
        var adv = DrillFactory.Build(DrillKind.AdvantageCapitalization, TinyLibrary());
        Assert.All(adv.Scenarios, s =>
            Assert.True(s.ThemeTags.Contains("advantage") || s.ThemeTags.Contains("crushing")));

        var def = DrillFactory.Build(DrillKind.Defender, TinyLibrary());
        Assert.NotEmpty(def.Scenarios);
        Assert.All(def.Scenarios, s => Assert.True(MaterialFromSideToMove(s.Fen) < 0));
    }

    [Fact]
    public void Drill_opening_selects_early_positions_and_build_is_deterministic()
    {
        var op = DrillFactory.Build(DrillKind.OpeningImprover, TinyLibrary());
        Assert.All(op.Scenarios, s => Assert.True(FullMove(s.Fen) <= 10));

        var a = DrillFactory.Build(DrillKind.Intuition, TinyLibrary(), count: 3);
        var b = DrillFactory.Build(DrillKind.Intuition, TinyLibrary(), count: 3);
        Assert.Equal(a.Scenarios.Select(s => s.Id), b.Scenarios.Select(s => s.Id));
        Assert.True(a.Scenarios.Count <= 3);
    }

    [Fact]
    public void BlunderPreventer_pairs_the_solution_with_a_distinct_alternative()
    {
        var problems = DrillFactory.BlunderPreventer(TinyLibrary(), count: 5);
        Assert.NotEmpty(problems);
        foreach (var p in problems)
        {
            Assert.NotEqual(p.BetterUci, p.WorseUci);
            Assert.Equal(2, p.Options.Count);
            Assert.Contains(p.BetterUci, p.Options);
            Assert.Contains(p.WorseUci, p.Options);
        }
    }

    // -- Ladder -------------------------------------------------------------

    [Fact]
    public void Ladder_unlocks_the_next_rung_only_after_a_win()
    {
        var ladder = new LadderProgression(startHints: 5);
        Assert.True(ladder.IsUnlocked(0));
        Assert.False(ladder.IsUnlocked(1));
        Assert.Equal(0, ladder.CurrentIndex);

        Assert.False(ladder.RecordResult(2, true));  // rung 2 is locked
        Assert.False(ladder.RecordResult(0, false)); // a loss unlocks nothing
        Assert.True(ladder.RecordResult(0, true));   // win on rung 0
        Assert.True(ladder.IsUnlocked(1));
        Assert.Equal(1, ladder.CurrentIndex);
        Assert.False(ladder.RecordResult(0, true));  // already beaten
    }

    [Fact]
    public void Ladder_hints_shrink_as_you_climb()
    {
        var ladder = new LadderProgression(startHints: 5);
        Assert.Equal(5, ladder.HintsFor(0));
        Assert.Equal(1, ladder.HintsFor(4));
        Assert.Equal(0, ladder.HintsFor(5));
        Assert.Equal(0, ladder.HintsFor(9));
    }

    // -- Expeditions --------------------------------------------------------

    [Fact]
    public void Expedition_catalog_maps_to_openings()
    {
        Assert.NotEmpty(Expeditions.Catalog);
        var sic = Expeditions.ById("exp_sicilian");
        Assert.NotNull(sic);
        Assert.Equal("sicilian", sic!.OpeningId);
        Assert.NotNull(OpeningLibrary.ById(sic.OpeningId));
    }

    [Fact]
    public void Expedition_run_completes_at_the_win_target()
    {
        var e = Expeditions.ById("exp_italian")!;
        var run = new ExpeditionRun(e);
        run.Record(true);
        run.Record(false);
        Assert.False(run.IsComplete);
        Assert.Equal(1, run.Wins);
        Assert.Equal(1, run.Losses);

        run.Record(true);
        run.Record(true); // reaches target of 3
        Assert.True(run.IsComplete);
        Assert.Equal(3, run.Wins);

        run.Record(true); // ignored once complete
        Assert.Equal(3, run.Wins);
        Assert.Equal(1.0, run.Progress);
    }

    // -- helpers mirroring DrillFactory's private reads (for assertions) -----

    private static int MaterialFromSideToMove(string fen)
    {
        var parts = fen.Split(' ');
        bool white = parts.Length < 2 || parts[1] == "w";
        int w = 0, b = 0;
        foreach (char c in parts[0])
        {
            int v = char.ToUpperInvariant(c) switch { 'P' => 1, 'N' => 3, 'B' => 3, 'R' => 5, 'Q' => 9, _ => 0 };
            if (v == 0) continue;
            if (char.IsUpper(c)) w += v; else b += v;
        }
        int diff = w - b;
        return white ? diff : -diff;
    }

    private static int FullMove(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length >= 6 && int.TryParse(parts[5], out int n) ? n : 1;
    }
}
