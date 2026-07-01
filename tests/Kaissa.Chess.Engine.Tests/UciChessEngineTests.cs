using Kaissa.Chess.Engine;
using Xunit;

namespace Kaissa.Chess.Engine.Tests;

public sealed class UciChessEngineTests
{
    private static UciChessEngine NewScriptedEngine() => new(new ScriptedUciTransport());

    [Fact]
    public async Task Handshake_reports_identity_and_options()
    {
        await using var engine = NewScriptedEngine();

        var id = await engine.HandshakeAsync();

        Assert.Equal("Kaissa.Scripted 1.0", id.Name);
        Assert.Equal("Kaissa", id.Author);
        Assert.Equal(3, id.Options.Count);
        Assert.True(id.SupportsElo);
        Assert.Equal("spin", id.Option("UCI_Elo")!.Type);
    }

    [Fact]
    public async Task IsReady_completes()
    {
        await using var engine = NewScriptedEngine();
        Assert.True(await engine.IsReadyAsync());
    }

    [Fact]
    public async Task Analyze_returns_best_move_ponder_and_evaluation()
    {
        await using var engine = NewScriptedEngine();

        var result = await engine.AnalyzeAsync("startpos", SearchLimits.ToDepth(12));

        Assert.Equal("e2e4", result.BestMove);
        Assert.Equal("e7e5", result.Ponder);
        Assert.Single(result.Lines);
        Assert.Equal(Score.Centipawns(34), result.Evaluation);
    }

    [Fact]
    public async Task Analyze_with_multipv_returns_ordered_lines()
    {
        await using var engine = NewScriptedEngine();

        var result = await engine.AnalyzeAsync("startpos", SearchLimits.ToDepth(12, multiPv: 3));

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result.Lines.Select(l => l.MultiPvIndex));
        Assert.Equal(Score.Centipawns(34), result.Lines[0].Score);
        Assert.Equal(Score.Centipawns(10), result.Lines[2].Score);
        Assert.Equal("d2d4", result.Lines[1].BestMove);
    }

    [Theory]
    [InlineData("info depth 20 multipv 1 score cp -45 nodes 1000 pv g1f3 d7d5", 1, 20, ScoreKind.Centipawns, -45, "g1f3")]
    [InlineData("info depth 30 seldepth 40 multipv 2 score mate 3 pv d1h5 g7g6", 2, 30, ScoreKind.Mate, 3, "d1h5")]
    public void TryParseInfo_reads_depth_multipv_score_and_pv(
        string line, int multiPv, int depth, ScoreKind kind, int value, string firstMove)
    {
        var pv = UciChessEngine.TryParseInfo(line);

        Assert.NotNull(pv);
        Assert.Equal(multiPv, pv!.MultiPvIndex);
        Assert.Equal(depth, pv.Depth);
        Assert.Equal(kind, pv.Score.Kind);
        Assert.Equal(value, pv.Score.Value);
        Assert.Equal(firstMove, pv.BestMove);
    }

    [Fact]
    public void TryParseInfo_ignores_lines_without_a_pv()
    {
        Assert.Null(UciChessEngine.TryParseInfo("info depth 1 score cp 20 nodes 30"));
    }

    [Fact]
    public void Score_formats_centipawns_and_mate()
    {
        Assert.Equal("+0.34", Score.Centipawns(34).ToString());
        Assert.Equal("-1.20", Score.Centipawns(-120).ToString());
        Assert.Equal("#3", Score.Mate(3).ToString());
    }
}
