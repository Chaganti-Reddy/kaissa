using System.Linq;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

/// <summary>Chunk-based SR, partial credit, and per-move spaced repetition.</summary>
public class CoreBatch4Tests
{
    // -- ChunkScheduler -----------------------------------------------------

    [Fact]
    public void ChunkScheduler_tracks_accuracy_per_chunk_and_finds_the_weakest()
    {
        var sch = new ChunkScheduler();
        // White to move, lone d-pawn -> tags include "Isolated pawn" and "Passed pawn" for White.
        const string iso = "4k3/8/8/8/3P4/8/8/4K3 w - - 0 1";
        sch.Record(iso, solved: true);
        sch.Record(iso, solved: false);
        sch.Record(iso, solved: false);

        var stat = sch.Snapshot().First(s => s.Chunk == ChunkTagger.IsolatedPawn);
        Assert.Equal(3, stat.Seen);
        Assert.Equal(1, stat.Correct);

        var weakest = sch.Weakest(n: 5, minSeen: 2);
        Assert.Contains(weakest, s => s.Chunk == ChunkTagger.IsolatedPawn);
    }

    [Fact]
    public void ChunkScheduler_snapshot_round_trips_through_the_seed()
    {
        var a = new ChunkScheduler();
        a.Record("4k3/8/8/8/3P4/8/8/4K3 w - - 0 1", true);
        var b = new ChunkScheduler(a.Snapshot());
        Assert.Equal(a.Snapshot().Count, b.Snapshot().Count);
        Assert.Equal(a.Snapshot().First().Seen, b.Snapshot().First().Seen);
    }

    // -- PartialCredit ------------------------------------------------------

    // White Qd1 can take the rook on d5 (best) or the knight on b3 (lesser but still winning material).
    private const string CaptureFen = "6k1/8/8/3r4/8/1n6/8/3Q2K1 w - - 0 1";

    [Fact]
    public void PartialCredit_exact_solution_is_correct()
    {
        Assert.Equal(PuzzleGrade.Correct, PartialCredit.Assess(CaptureFen, "d1d5", new[] { "d1d5" }));
    }

    [Fact]
    public void PartialCredit_a_bigger_or_equal_winning_capture_is_partial()
    {
        // Solution wins the knight (3); playing Qxd5 wins the rook (5) instead -> partial, not wrong.
        Assert.Equal(PuzzleGrade.Partial, PartialCredit.Assess(CaptureFen, "d1d5", new[] { "d1b3" }));
    }

    [Fact]
    public void PartialCredit_a_smaller_capture_or_a_quiet_move_is_wrong()
    {
        // Solution wins the rook (5); Qxb3 only wins the knight (3) -> wrong.
        Assert.Equal(PuzzleGrade.Wrong, PartialCredit.Assess(CaptureFen, "d1b3", new[] { "d1d5" }));
        // A quiet move captures nothing -> wrong.
        Assert.Equal(PuzzleGrade.Wrong, PartialCredit.Assess(CaptureFen, "d1d2", new[] { "d1d5" }));
    }

    [Fact]
    public void PartialCredit_illegal_move_is_wrong()
    {
        Assert.Equal(PuzzleGrade.Wrong, PartialCredit.Assess(CaptureFen, "d1d8", new[] { "d1d5" }));
    }

    // -- MoveTrainer --------------------------------------------------------

    [Fact]
    public void MoveTrainer_schedules_each_move_independently()
    {
        var mt = new MoveTrainer();
        mt.Add("L1", new[] { "e2e4", "e7e5", "g1f3" });
        Assert.Equal(3, mt.Count);

        // All new -> all due at day 0.
        Assert.Equal(3, mt.Due(0).Count);

        // Get one right; it advances and is no longer due today, the others still are.
        mt.Review("L1", 0, correct: true, today: 0);
        Assert.True(mt.BoxOf("L1", 0) > 1);
        Assert.DoesNotContain(mt.Due(0), i => i.MoveIndex == 0);
        Assert.Equal(2, mt.Due(0).Count);

        // A wrong review sends a move back to box 1 (due one day out on the ladder).
        mt.Review("L1", 1, correct: false, today: 0);
        Assert.Equal(1, mt.BoxOf("L1", 1));
        Assert.Contains(mt.Due(1), i => i.MoveIndex == 1);
    }

    [Fact]
    public void MoveTrainer_next_due_prefers_the_lowest_box()
    {
        var mt = new MoveTrainer();
        mt.Add("L1", new[] { "e2e4", "e7e5" });
        mt.Review("L1", 0, correct: true, today: 0); // move 0 -> box 2
        var next = mt.NextDue(0);
        Assert.NotNull(next);
        Assert.Equal(1, next!.MoveIndex); // move 1 still at box 1, so it comes first
    }
}
