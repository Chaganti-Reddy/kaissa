using System;
using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class OpeningRepertoireTests
{
    private static readonly DateTime Origin = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RepertoireLine Italian =>
        new("italian", "Italian Game", Side.White, new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1c4" }, "Open games (1.e4 e5)");

    [Fact]
    public void Only_the_players_moves_become_decisions()
    {
        var session = new RepertoireSession(new[] { Italian }, new OpeningProgress(), new ManualClock(Origin));
        Assert.Equal(3, session.Total); // White's moves: e2e4, g1f3, f1c4

        var black = new RepertoireLine("sicilian", "Sicilian", Side.Black,
            new[] { "e2e4", "c7c5", "g1f3", "d7d6" }, "Sicilian structures");
        var bsession = new RepertoireSession(new[] { black }, new OpeningProgress(), new ManualClock(Origin));
        Assert.Equal(2, bsession.Total); // Black's moves: c7c5, d7d6
    }

    [Fact]
    public void The_first_card_is_the_opening_move_for_the_players_side()
    {
        var session = new RepertoireSession(new[] { Italian }, new OpeningProgress(), new ManualClock(Origin));
        var card = session.Next()!;
        Assert.True(card.WhiteToMove);
        Assert.Equal("e2e4", card.ExpectedMove);
    }

    [Fact]
    public void A_correct_recall_schedules_a_future_review()
    {
        var progress = new OpeningProgress();
        var session = new RepertoireSession(new[] { Italian }, progress, new ManualClock(Origin));
        session.Next();
        var result = session.Submit("e2e4", TimeSpan.FromSeconds(2));
        Assert.True(result.Correct);
        Assert.True(result.IntervalDays > 0);
        Assert.Equal(0, session.DueCount); // just scheduled into the future, not due now
    }

    [Fact]
    public void A_wrong_recall_reports_the_book_move_and_is_a_lapse()
    {
        var progress = new OpeningProgress();
        var session = new RepertoireSession(new[] { Italian }, progress, new ManualClock(Origin));
        session.Next();
        var result = session.Submit("a2a3", TimeSpan.FromSeconds(2)); // legal, but not the book move
        Assert.False(result.Correct);
        Assert.Equal("e2e4", result.ExpectedMove);
    }

    [Fact]
    public void The_default_repertoire_has_the_expected_number_of_decisions()
    {
        var session = new RepertoireSession(OpeningRepertoire.Default, new OpeningProgress(), new ManualClock(Origin));
        // italian 3 + ruy 3 + sicilian 3 + caro 2 + french 2 + queen's gambit 2 + london 3 + king's indian 3
        Assert.Equal(21, session.Total);
    }

    [Fact]
    public void A_new_card_is_level_zero_and_carries_its_chunk()
    {
        var session = new RepertoireSession(new[] { Italian }, new OpeningProgress(), new ManualClock(Origin));
        var card = session.Next()!;
        Assert.Equal(0, card.Level); // never seen
        Assert.Equal("Open games (1.e4 e5)", card.Chunk);
    }

    [Fact]
    public void A_correct_recall_raises_the_level_and_labels_the_next_review()
    {
        var progress = new OpeningProgress();
        var session = new RepertoireSession(new[] { Italian }, progress, new ManualClock(Origin));
        session.Next();
        var result = session.Submit("e2e4", TimeSpan.FromSeconds(2));
        Assert.True(result.Level >= 1);
        Assert.False(string.IsNullOrEmpty(result.NextLabel));
    }

    [Fact]
    public void Progress_survives_a_save_load_round_trip()
    {
        var progress = new OpeningProgress();
        var session = new RepertoireSession(new[] { Italian }, progress, new ManualClock(Origin));
        session.Next();
        session.Submit("e2e4", TimeSpan.FromSeconds(2));

        var reloaded = OpeningProgress.FromJson(progress.ToJson());
        Assert.Equal(progress.Count, reloaded.Count);
        var s = reloaded.GetOrCreate("italian:0");
        Assert.True(s.Seen);
        Assert.NotNull(s.DueUtc);
    }
}
