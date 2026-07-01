using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Import;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class LichessImportTests
{
    // PuzzleId,FEN,Moves,Rating,RatingDeviation,Popularity,NbPlays,Themes,GameUrl,OpeningTags
    private const string SampleRow =
        "00sHx,rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1,e2e4 e7e5,1200,80,95,1000,fork short,https://lichess.org/x,";

    [Fact]
    public void Parses_the_fields_of_a_row()
    {
        Assert.True(LichessPuzzleParser.TryParseRow(SampleRow, out var puzzle));
        Assert.Equal("00sHx", puzzle.PuzzleId);
        Assert.Equal(1200, puzzle.Rating);
        Assert.Equal(new[] { "e2e4", "e7e5" }, puzzle.Moves);
        Assert.Contains("fork", puzzle.Themes);
    }

    [Fact]
    public void Maps_themes_to_a_pattern_by_priority()
    {
        // pin outranks fork in the priority list.
        Assert.Equal(new PatternId("tactic.pin"), LichessPuzzleParser.MapPattern(new[] { "fork", "pin" }));
        Assert.Equal(new PatternId("checkmate.back_rank"), LichessPuzzleParser.MapPattern(new[] { "backRankMate", "mate" }));
        Assert.Null(LichessPuzzleParser.MapPattern(new[] { "endgame", "long" }));
    }

    [Fact]
    public void Builds_a_scenario_from_the_position_after_the_setup_move()
    {
        Assert.True(LichessPuzzleParser.TryParseRow(SampleRow, out var puzzle));
        Assert.True(LichessPuzzleParser.TryBuildScenario(puzzle, out var scenario, out _));

        Assert.Equal(new PatternId("tactic.fork"), scenario.Pattern);
        // The solution is the solver's move (Moves[1]), not the opponent's setup move (Moves[0]).
        Assert.Equal(new[] { "e7e5" }, scenario.Solutions);
        // Scenario position is after the setup move, so it is Black to move here.
        Assert.Equal(Side.Black, ChessGame.FromFen(scenario.Fen).SideToMove);
    }

    [Fact]
    public void Rejects_puzzles_without_a_supported_theme_or_enough_moves()
    {
        var noTheme = new ImportedPuzzle("x", ChessGame.StartFen, new[] { "e2e4", "e7e5" }, 1000, new[] { "endgame" });
        Assert.False(LichessPuzzleParser.TryBuildScenario(noTheme, out _, out var reason1));
        Assert.Equal("no supported theme", reason1);

        var shortMoves = new ImportedPuzzle("y", ChessGame.StartFen, new[] { "e2e4" }, 1000, new[] { "fork" });
        Assert.False(LichessPuzzleParser.TryBuildScenario(shortMoves, out _, out var reason2));
        Assert.Equal("not enough moves", reason2);
    }

    [Fact]
    public void Every_catalog_pattern_has_metadata()
    {
        Assert.NotEmpty(LichessPuzzleParser.Catalog);
        Assert.All(LichessPuzzleParser.Catalog.Values, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
        });
    }
}
