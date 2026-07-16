using Kaissa.Chess.Rules;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class MasterGamesTests
{
    [Fact]
    public void Every_bundled_game_is_legal_from_start_to_finish()
    {
        foreach (var g in MasterGames.All)
        {
            var board = ChessGame.Start();
            foreach (var mv in g.Moves)
                Assert.True(board.TryMakeMove(mv), $"{g.Id}: illegal move {mv}");
        }
    }

    [Fact]
    public void A_correct_guess_scores_and_a_wrong_guess_reveals_the_master_move()
    {
        var s = new GuessMoveSession(MasterGames.ById("opera")!);
        Assert.True(s.PlayerToMove);

        var first = s.Guess("e2e4"); // Morphy's actual first move
        Assert.True(first.Correct);
        Assert.Equal(1, s.Score);

        var second = s.Guess("a2a3"); // not the master move
        Assert.False(second.Correct);
        Assert.False(string.IsNullOrEmpty(second.ActualUci)); // the real move is revealed
        Assert.Equal(1, s.Score);
        Assert.Equal(2, s.Answered);
    }

    [Fact]
    public void The_session_advances_to_the_end_and_asks_every_player_move()
    {
        foreach (var g in MasterGames.All)
        {
            var s = new GuessMoveSession(g);
            int total = s.TotalGuesses;
            Assert.True(total > 0);

            int guard = 100;
            while (!s.Done && guard-- > 0)
            {
                Assert.True(s.PlayerToMove);
                s.Guess("a1a1"); // never the master move - forces a reveal and advances the game
            }
            Assert.True(s.Done);
            Assert.Equal(total, s.Answered);
        }
    }
}
