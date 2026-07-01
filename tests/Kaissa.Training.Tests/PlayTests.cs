using Kaissa.Chess.Engine;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class PlayTests
{
    private static string? EnginePath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("KAISSA_STOCKFISH_PATH");
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
        }
    }

    [Fact]
    public void A_draw_against_an_equal_opponent_leaves_the_rating_unchanged()
    {
        Assert.Equal(1000, RatingEstimator.Update(1000, 1000, 0.5), 6);
        Assert.True(RatingEstimator.Update(1000, 1000, 1.0) > 1000); // win
        Assert.True(RatingEstimator.Update(1000, 1000, 0.0) < 1000); // loss
    }

    [Fact]
    public void Target_elo_tracks_the_player_and_clamps_to_the_engine_range()
    {
        var opponent = new AdaptiveOpponent(engine: null!);
        Assert.Equal(1500, opponent.TargetElo(1500));
        Assert.Equal(1320, opponent.TargetElo(500));   // below the engine's floor
        Assert.Equal(3190, opponent.TargetElo(5000));  // above the ceiling
    }

    [Fact]
    public async Task Opponent_returns_a_legal_move()
    {
        var path = EnginePath;
        if (path is null)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var engine = UciChessEngine.LaunchProcess(path);
        await engine.HandshakeAsync(cts.Token);
        await engine.NewGameAsync(cts.Token);

        var opponent = new AdaptiveOpponent(engine, TimeSpan.FromMilliseconds(50));
        var game = ChessGame.Start();

        var move = await opponent.ChooseMoveAsync(game.Fen, playerRating: 1500, cancellationToken: cts.Token);

        Assert.Contains(move, game.LegalUciMoves());
    }

    [Fact]
    public async Task A_game_against_the_engine_plays_to_a_finish()
    {
        var path = EnginePath;
        if (path is null)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var engine = UciChessEngine.LaunchProcess(path);
        await engine.HandshakeAsync(cts.Token);
        await engine.NewGameAsync(cts.Token);

        var opponent = new AdaptiveOpponent(engine, TimeSpan.FromMilliseconds(50));
        // Player is White making only dummy moves; a strong opponent should win within the cap.
        var game = new GameSession(engine, Side.White, playerRating: 2600, opponent: opponent);

        int plies = 0;
        while (!game.IsGameOver && plies < 300)
        {
            if (game.SideToMove == Side.White)
                Assert.True(game.TryPlayerMove(game.LegalUciMoves()[0]));
            else
                Assert.NotNull(await game.EngineReplyAsync(cts.Token));
            plies++;
        }

        Assert.True(game.IsGameOver, $"game did not finish within {plies} plies");
        Assert.NotEqual(GameResult.Ongoing, game.Result);

        var before = game.PlayerRating;
        game.FinalizeRating();
        Assert.InRange(game.PlayerRating, 100, 3000); // rating stays valid after the result
        _ = before;
    }
}
