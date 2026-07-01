using Kaissa.Chess.Engine;
using Kaissa.Chess.Rules;
using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class GameReviewTests
{
    private static string? EnginePath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("KAISSA_STOCKFISH_PATH");
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
        }
    }

    [Theory]
    [InlineData(0, MoveQuality.Best)]
    [InlineData(20, MoveQuality.Best)]
    [InlineData(45, MoveQuality.Good)]
    [InlineData(90, MoveQuality.Inaccuracy)]
    [InlineData(150, MoveQuality.Mistake)]
    [InlineData(500, MoveQuality.Blunder)]
    public void Classifies_moves_by_centipawn_loss(int loss, MoveQuality expected)
    {
        Assert.Equal(expected, MoveClassifier.Classify(loss));
    }

    [Fact]
    public void Converts_a_mate_score_to_a_large_signed_centipawn_value()
    {
        Assert.Equal(99_900, MoveClassifier.ToCentipawns(Score.Mate(1)));
        Assert.True(MoveClassifier.ToCentipawns(Score.Mate(-2)) < -50_000);
        Assert.Equal(35, MoveClassifier.ToCentipawns(Score.Centipawns(35)));
    }

    [Fact]
    public async Task A_blunder_in_a_game_becomes_a_solvable_practice_puzzle()
    {
        var path = EnginePath;
        if (path is null)
            return;

        // White has mate in one (Ra8#) but plays a quiet king move instead.
        const string fen = "6k1/5ppp/8/8/8/8/8/R6K w - - 0 1";
        var moves = new[] { "h1g1" };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var engine = UciChessEngine.LaunchProcess(path);
        await engine.HandshakeAsync(cts.Token);

        var analyzer = new GameAnalyzer(engine, depth: 14);
        var assessments = await analyzer.AnalyzeAsync(fen, moves, Side.White, cts.Token);

        var assessment = Assert.Single(assessments);
        Assert.Equal(MoveQuality.Blunder, assessment.Quality);
        Assert.Equal("a1a8", assessment.BestMove);

        // The blunder is turned into a practice scenario whose solution is the move that was missed.
        var practice = GamePractice.FromAssessments(assessments);
        var scenario = Assert.Single(practice);
        Assert.Equal(new[] { "a1a8" }, scenario.Solutions);
        Assert.Equal(fen, scenario.Fen);

        // And that scenario is actually solvable under the rules.
        Assert.NotNull(ChessGame.FromFen(scenario.Fen).ResolveToUci(scenario.Solutions[0]));
    }
}
