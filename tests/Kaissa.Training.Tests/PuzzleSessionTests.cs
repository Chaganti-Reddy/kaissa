using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class PuzzleSessionTests
{
    private static readonly PatternId Fork = new("tactic.fork");

    // A deterministic four-ply line from the start position: solver e2e4, opp e7e5,
    // solver g1f3, opp b8c6. All legal, no engine needed.
    private static Scenario MultiMove() => new(
        "test-multi", Fork, ChessGame_StartFen,
        new[] { "e2e4" }, "White to move.", 1200,
        Line: new[] { "e2e4", "e7e5", "g1f3", "b8c6" });

    private const string ChessGame_StartFen =
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public void Single_move_scenario_solves_in_one_correct_move()
    {
        var s = new Scenario("test-single", Fork, ChessGame_StartFen,
            new[] { "e2e4" }, "White to move.", 1000);
        var session = new PuzzleSession(s);

        Assert.Equal(1, session.SolverMovesTotal);
        var r = session.Submit("e2e4");

        Assert.Equal(PuzzleOutcome.Solved, r.Outcome);
        Assert.True(session.Solved);
        Assert.Null(r.ReplyMove);
    }

    [Fact]
    public void Wrong_move_leaves_position_unchanged()
    {
        var session = new PuzzleSession(MultiMove());
        var before = session.Fen;

        var r = session.Submit("a2a3"); // legal but not the solution

        Assert.Equal(PuzzleOutcome.Wrong, r.Outcome);
        Assert.False(session.Solved);
        Assert.Equal(before, session.Fen);
        Assert.Equal("e2e4", r.ExpectedMove);
    }

    [Fact]
    public void Correct_move_plays_opponent_reply_and_continues()
    {
        var session = new PuzzleSession(MultiMove());

        var r = session.Submit("e2e4");

        Assert.Equal(PuzzleOutcome.Continue, r.Outcome);
        Assert.Equal("e7e5", r.ReplyMove);
        Assert.Equal(1, session.SolverMovesDone);
        Assert.False(session.Solved);
        // Board has advanced through both plies; it's White to move again.
        Assert.Contains(" w ", session.Fen);
    }

    [Fact]
    public void Full_line_solves_and_final_move_has_no_reply()
    {
        var session = new PuzzleSession(MultiMove());

        session.Submit("e2e4");           // -> e7e5
        var r = session.Submit("g1f3");   // -> b8c6, completes the line

        Assert.Equal(PuzzleOutcome.Solved, r.Outcome);
        Assert.Equal("b8c6", r.ReplyMove);
        Assert.True(session.Solved);
        Assert.Equal(2, session.SolverMovesDone);
        Assert.Null(session.ExpectedMove);
    }

    [Fact]
    public void Wrong_mid_line_move_does_not_advance()
    {
        var session = new PuzzleSession(MultiMove());
        session.Submit("e2e4"); // now expecting g1f3
        var mid = session.Fen;

        var r = session.Submit("d2d4"); // legal, not the solution

        Assert.Equal(PuzzleOutcome.Wrong, r.Outcome);
        Assert.Equal(mid, session.Fen);
        Assert.Equal("g1f3", r.ExpectedMove);
    }

    [Fact]
    public void Setup_and_start_fen_are_exposed()
    {
        var s = MultiMove() with { Setup = "d7d5" };
        var session = new PuzzleSession(s);

        Assert.Equal("d7d5", session.SetupMove);
        Assert.Equal(ChessGame_StartFen, session.StartFen);
    }
}
