using Kaissa.Chess.Rules;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class EndgameDrillTests
{
    [Fact]
    public void Every_endgame_fen_is_legal_and_the_player_can_move()
    {
        Assert.NotEmpty(EndgameLibrary.All);
        foreach (var e in EndgameLibrary.All)
        {
            var game = ChessGame.FromFen(e.Fen); // throws if illegal
            Assert.False(game.IsGameOver, $"{e.Id} starts already over");
            Assert.NotEmpty(game.LegalUciMoves());

            // Legal position: the side NOT to move must not be in check (else it's the wrong turn).
            var parts = e.Fen.Split(' ');
            parts[1] = parts[1] == "w" ? "b" : "w";
            Assert.False(ChessGame.FromFen(string.Join(' ', parts)).IsCheck,
                $"{e.Id} has the side-not-to-move in check (illegal)");
        }
    }

    [Fact]
    public void Categories_cover_every_endgame()
    {
        foreach (var cat in EndgameLibrary.Categories)
            Assert.NotEmpty(EndgameLibrary.InCategory(cat));
        int grouped = EndgameLibrary.Categories.Sum(c => EndgameLibrary.InCategory(c).Count);
        Assert.Equal(EndgameLibrary.All.Count, grouped);
    }

    [Fact]
    public void Win_goal_is_ongoing_until_mate()
    {
        var kq = EndgameLibrary.ById("mate_kq")!;
        Assert.Equal(DrillOutcome.Ongoing, DrillEvaluator.Evaluate(kq.Fen, kq.PlayerWhite, kq.Goal));
    }

    [Fact]
    public void Win_goal_passes_when_the_player_delivers_mate()
    {
        // Black king h8 checkmated by Qg7 (protected by Kg6). Mated side = side to move = Black.
        const string blackMated = "7k/6Q1/6K1/8/8/8/8/8 b - - 0 1";
        Assert.Equal(DrillOutcome.Passed, DrillEvaluator.Evaluate(blackMated, playerWhite: true, DrillGoal.Win));
    }

    [Fact]
    public void Win_goal_fails_when_the_player_is_mated()
    {
        const string whiteMated = "8/8/8/8/8/6k1/6q1/7K w - - 0 1"; // White (to move) checkmated by Qg2/Kg3
        Assert.Equal(DrillOutcome.Failed, DrillEvaluator.Evaluate(whiteMated, playerWhite: true, DrillGoal.Win));
    }

    [Fact]
    public void Win_goal_fails_on_a_draw()
    {
        const string stalemate = "7k/8/6Q1/8/8/8/8/6K1 b - - 0 1"; // Black to move, stalemated
        Assert.Equal(DrillOutcome.Failed, DrillEvaluator.Evaluate(stalemate, playerWhite: true, DrillGoal.Win));
    }

    [Fact]
    public void Draw_goal_passes_on_a_draw()
    {
        const string stalemate = "7k/8/6Q1/8/8/8/8/6K1 b - - 0 1";
        Assert.Equal(DrillOutcome.Passed, DrillEvaluator.Evaluate(stalemate, playerWhite: false, DrillGoal.Draw));
    }

    [Fact]
    public void Promote_goal_passes_once_a_queen_appears()
    {
        const string promoted = "4Q3/8/4k3/8/8/8/8/4K3 b - - 0 1"; // White just promoted
        Assert.Equal(DrillOutcome.Passed, DrillEvaluator.Evaluate(promoted, playerWhite: true, DrillGoal.Promote));
    }
}
