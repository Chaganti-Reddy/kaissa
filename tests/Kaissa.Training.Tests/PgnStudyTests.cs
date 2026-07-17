using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class PgnStudyTests
{
    [Fact]
    public void Reads_headers_and_the_mainline_moves()
    {
        const string pgn = "[Event \"Italian Game\"]\n[White \"Teacher\"]\n[Black \"Student\"]\n\n1. e4 e5 2. Nf3 Nc6 3. Bc4 *";
        var chapter = PgnStudy.Parse(pgn);
        Assert.Equal("Italian Game", chapter.Title);
        Assert.Equal(5, chapter.Moves.Count);
        Assert.Equal("e4", chapter.Moves[0].San);
        Assert.Equal("e2e4", chapter.Moves[0].Uci);
    }

    [Fact]
    public void Every_parsed_move_is_legal_from_the_start()
    {
        const string pgn = "1. e4 c5 2. Nf3 d6 3. d4 cxd4 4. Nxd4 Nf6 5. Nc3 a6";
        var chapter = PgnStudy.Parse(pgn);
        var game = ChessGame.Start();
        foreach (var mv in chapter.Moves)
            Assert.True(game.TryMakeMove(mv.Uci), $"illegal {mv.San}");
        Assert.Equal(10, chapter.Moves.Count);
    }

    [Fact]
    public void Comments_attach_to_the_preceding_move()
    {
        const string pgn = "1. e4 {best by test} e5 2. Nf3 {develops and attacks} Nc6";
        var chapter = PgnStudy.Parse(pgn);
        Assert.Equal("best by test", chapter.Moves[0].Comment);
        Assert.Equal("develops and attacks", chapter.Moves[2].Comment);
    }

    [Fact]
    public void Variations_and_nags_and_results_are_ignored()
    {
        const string pgn = "1. e4 e5 2. Nf3 (2. Bc4 Nf6) Nc6 $1 3. Bb5 1-0";
        var chapter = PgnStudy.Parse(pgn);
        // Mainline is e4 e5 Nf3 Nc6 Bb5 = 5 moves; the (2. Bc4 Nf6) variation is skipped.
        Assert.Equal(5, chapter.Moves.Count);
        Assert.Equal("Bb5", chapter.Moves[4].San);
    }
}
