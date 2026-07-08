using Kaissa.Training.Api;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class AnalysisSessionTests
{
    [Fact]
    public void A_new_session_starts_at_the_initial_position()
    {
        var session = new AnalysisSession();
        Assert.Equal(0, session.Ply);
        Assert.False(session.CanStepBack);
        Assert.False(session.CanStepForward);
        Assert.True(session.WhiteToMove);
    }

    [Fact]
    public void Playing_a_move_advances_the_line()
    {
        var session = new AnalysisSession();
        Assert.True(session.Play("e2e4"));
        Assert.Equal(1, session.Ply);
        Assert.True(session.CanStepBack);
        Assert.False(session.CanStepForward);
        Assert.False(session.WhiteToMove); // black to move after 1.e4
    }

    [Fact]
    public void An_illegal_move_is_rejected()
    {
        var session = new AnalysisSession();
        Assert.False(session.Play("e2e5")); // no such pawn push
        Assert.Equal(0, session.Ply);
    }

    [Fact]
    public void Stepping_back_and_forward_walks_the_line_without_losing_it()
    {
        var session = new AnalysisSession();
        session.Play("e2e4");
        session.Play("e7e5");
        Assert.Equal(2, session.Ply);

        Assert.True(session.StepBack());
        Assert.Equal(1, session.Ply);
        Assert.Equal(2, session.LineLength); // the move is still there, just not applied
        Assert.True(session.CanStepForward);

        Assert.True(session.StepForward());
        Assert.Equal(2, session.Ply);
    }

    [Fact]
    public void Playing_from_a_stepped_back_position_branches_and_discards_the_old_continuation()
    {
        var session = new AnalysisSession();
        session.Play("e2e4");
        session.Play("e7e5");
        session.StepBack(); // back to after 1.e4, black to move

        Assert.True(session.Play("c7c5")); // Sicilian instead of ...e5
        Assert.Equal(2, session.Ply);
        Assert.Equal(2, session.LineLength);      // old ...e5 dropped
        Assert.Equal("c5", session.LineSan()[1]);
    }

    [Fact]
    public void Line_san_reflects_the_moves_played()
    {
        var session = new AnalysisSession();
        session.Play("e2e4");
        session.Play("e7e5");
        session.Play("g1f3");
        Assert.Equal(new[] { "e4", "e5", "Nf3" }, session.LineSan());
    }

    [Fact]
    public void Loading_a_fen_resets_the_session()
    {
        var session = new AnalysisSession();
        session.Play("e2e4");
        session.LoadFen("8/8/8/4k3/8/8/4P3/4K3 w - - 0 1");
        Assert.Equal(0, session.Ply);
        Assert.Equal(0, session.LineLength);
        Assert.True(session.Play("e2e4")); // legal from the loaded position
    }
}
