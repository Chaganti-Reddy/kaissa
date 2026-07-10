using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class LessonTests
{
    private static readonly ScenarioLibrary Lib = ScenarioLibrary.LoadDefault();

    [Fact]
    public void Lessons_exist_and_group_by_topic()
    {
        Assert.NotEmpty(LessonLibrary.All);
        Assert.Contains("Tactics", LessonLibrary.Topics);
        Assert.Contains("Checkmates", LessonLibrary.Topics);
        foreach (var t in LessonLibrary.Topics)
            Assert.NotEmpty(LessonLibrary.ByTopic(t));
    }

    [Fact]
    public void Every_lesson_pattern_has_content_in_the_library()
    {
        foreach (var l in LessonLibrary.All)
            Assert.NotEmpty(Lib.ForPattern(l.Pattern));
    }

    [Fact]
    public void A_session_has_an_intro_then_interactive_challenges()
    {
        var lesson = LessonLibrary.ById("fork")!;
        var session = new LessonSession(lesson, Lib, seed: 1);

        Assert.True(session.Count >= 2);
        Assert.False(session[0].Interactive);                 // intro observes
        Assert.Equal(session.Count, session[0].Total);

        for (int i = 1; i < session.Count; i++)
        {
            Assert.True(session[i].Interactive);              // challenges ask for a move
            Assert.False(string.IsNullOrEmpty(session[i].ExpectedMove));
            Assert.NotEqual(session[0].Fen, session[i].Fen);  // never re-show the intro's position as a challenge
        }
    }

    [Fact]
    public void Challenge_positions_are_legal_and_the_expected_move_is_legal()
    {
        foreach (var l in LessonLibrary.All)
        {
            var s = new LessonSession(l, Lib, seed: 7);
            for (int i = 1; i < s.Count; i++)
            {
                var g = ChessGame.FromFen(s[i].Fen);
                Assert.False(g.IsGameOver);
                Assert.Contains(s[i].ExpectedMove, g.LegalUciMoves());
            }
        }
    }

    [Fact]
    public void Challenge_count_respects_the_lesson_setting()
    {
        var lesson = LessonLibrary.ById("smothered")!; // 3 challenges
        var s = new LessonSession(lesson, Lib);
        Assert.Equal(lesson.Challenges + 1, s.Count); // + intro
    }
}
