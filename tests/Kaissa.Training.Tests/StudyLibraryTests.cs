using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class StudyLibraryTests
{
    [Fact]
    public void Every_bundled_study_parses_to_a_legal_mainline()
    {
        Assert.NotEmpty(StudyLibrary.Chapters);
        foreach (var chapter in StudyLibrary.Chapters)
        {
            Assert.False(string.IsNullOrWhiteSpace(chapter.Title));
            Assert.NotEmpty(chapter.Moves);

            var game = ChessGame.Start();
            foreach (var mv in chapter.Moves)
                Assert.True(game.TryMakeMove(mv.Uci), $"illegal {mv.San} in '{chapter.Title}'");
        }
    }

    [Fact]
    public void Comments_are_attached_and_titles_come_from_headers()
    {
        var chapters = StudyLibrary.Chapters;
        Assert.Contains(chapters, c => c.Title == "Italian Game: main ideas");
        Assert.Contains(chapters, c => c.Title == "Legal's Mate: a classic trap");
        // At least some moves carry teaching comments.
        Assert.Contains(chapters.SelectMany(c => c.Moves), m => !string.IsNullOrEmpty(m.Comment));
    }
}
