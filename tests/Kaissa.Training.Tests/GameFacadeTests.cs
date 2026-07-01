using Kaissa.Chess.Rules;
using Kaissa.Training.Api;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class GameFacadeTests
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
    public async Task Playing_a_move_gets_a_bot_reply_and_updates_the_board()
    {
        var path = EnginePath;
        if (path is null)
            return;

        await using var game = await KaissaGame.StartAsync(path, Side.White, playerRating: 1200,
            botThinkTime: TimeSpan.FromMilliseconds(50));

        var outcome = await game.PlayAsync("e2e4");

        Assert.True(outcome.Accepted);
        Assert.Equal("e2e4", outcome.PlayerMove);
        Assert.False(outcome.IsGameOver);
        Assert.False(string.IsNullOrEmpty(outcome.BotMove)); // bot replied
        Assert.NotEmpty(outcome.Board.Pieces);
    }

    [Fact]
    public async Task An_illegal_move_is_rejected_and_leaves_the_position_unchanged()
    {
        var path = EnginePath;
        if (path is null)
            return;

        await using var game = await KaissaGame.StartAsync(path, Side.White, 1200,
            botThinkTime: TimeSpan.FromMilliseconds(50));

        var outcome = await game.PlayAsync("e2e5"); // three-square pawn push: illegal
        Assert.False(outcome.Accepted);
        Assert.True(outcome.Board.WhiteToMove); // still White to move
    }

    [Fact]
    public async Task Playing_as_black_lets_the_bot_open()
    {
        var path = EnginePath;
        if (path is null)
            return;

        await using var game = await KaissaGame.StartAsync(path, Side.Black, 1200,
            botThinkTime: TimeSpan.FromMilliseconds(50));

        // The bot (White) has already moved, so it is the player's (Black's) turn.
        Assert.False(game.Board.WhiteToMove);
    }

    [Fact]
    public async Task Reviewing_a_short_game_returns_without_error()
    {
        var path = EnginePath;
        if (path is null)
            return;

        await using var game = await KaissaGame.StartAsync(path, Side.White, 1200,
            botThinkTime: TimeSpan.FromMilliseconds(50));
        await game.PlayAsync("e2e4");
        await game.PlayAsync("g1f3");

        var review = await game.ReviewAsync();
        Assert.NotNull(review.Mistakes);
        Assert.NotNull(review.Practice);
    }
}
