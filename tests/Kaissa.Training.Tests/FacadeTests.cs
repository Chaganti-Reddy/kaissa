using Kaissa.Training;
using Kaissa.Training.Api;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class FacadeTests
{
    private static readonly DateTime Origin = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string BackRankFen = "6k1/5ppp/8/8/8/8/8/R6K w - - 0 1";

    [Fact]
    public void Board_view_reads_the_start_position()
    {
        var board = BoardView.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

        Assert.Equal(32, board.Pieces.Count);
        Assert.True(board.WhiteToMove);
        Assert.Contains(new BoardSquare("e1", 'K'), board.Pieces);
        Assert.Contains(new BoardSquare("e8", 'k'), board.Pieces);
    }

    [Fact]
    public void The_trainer_presents_a_card_grades_an_answer_and_tracks_progress()
    {
        var trainer = SingleScenarioTrainer();

        var card = trainer.NextCard();
        Assert.NotNull(card);
        Assert.Equal("Back-rank mate", card!.PatternName);
        Assert.Equal(900, card.PuzzleRating);
        Assert.NotEmpty(card.Board.Pieces);

        var result = trainer.Answer("a1a8", TimeSpan.FromSeconds(5));
        Assert.True(result.Correct);
        Assert.True(result.PlayerRatingChange > 0);
        Assert.True(result.NextReviewInDays >= 1);
        Assert.Contains("a1a8", result.Solutions);

        var progress = trainer.Progress();
        Assert.Contains(progress, r => r.PatternId == "checkmate.back_rank" && r.Seen && r.Reps == 1);
        Assert.False(string.IsNullOrWhiteSpace(trainer.ExportProgress()));
    }

    [Fact]
    public void Answering_before_a_card_is_dealt_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => SingleScenarioTrainer().Answer("a1a8", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Default_trainer_loads_the_bundled_content()
    {
        var trainer = KaissaTrainer.CreateDefault();
        Assert.NotNull(trainer.NextCard());
    }

    private static KaissaTrainer SingleScenarioTrainer()
    {
        var pattern = new Pattern(new PatternId("checkmate.back_rank"), "Back-rank mate", "Mate on the back rank.");
        var scenario = new Scenario("s1", pattern.Id, BackRankFen, new[] { "a1a8" }, "White mates in one.", 900);
        var library = new ScenarioLibrary(new[] { pattern }, new[] { scenario });
        return new KaissaTrainer(library, new SkillModel(), new ManualClock(Origin));
    }
}
