using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public sealed class BatchThreeTests
{
    // --- Coordinates ---

    [Fact]
    public void Coordinate_naming_round_trips()
    {
        Assert.Equal("e4", Coordinates.Name(4, 3));
        Assert.Equal((4, 3), Coordinates.Parse("e4"));
    }

    [Fact]
    public void Coordinate_session_scores_the_asked_square()
    {
        var session = new CoordinateSession();
        var name = session.NextTarget();
        var (file, rank) = Coordinates.Parse(name);

        Assert.True(session.Answer(file, rank));
        Assert.Equal(1, session.Score);

        session.NextTarget();
        Assert.False(session.Answer(-1, -1));
        Assert.Equal(2, session.Asked);
    }

    // --- Opening trainer ---

    [Fact]
    public void Opening_trainer_walks_a_line_move_by_move()
    {
        var line = OpeningLibrary.ById("italian")!;
        var trainer = new OpeningTrainer(line);

        foreach (var move in line.Moves)
        {
            Assert.Equal(move, trainer.ExpectedMove);
            Assert.True(trainer.Play(move), $"move {move} should be accepted");
        }

        Assert.True(trainer.IsComplete);
        Assert.Null(trainer.ExpectedMove);
    }

    [Fact]
    public void Opening_trainer_rejects_a_wrong_move()
    {
        var trainer = new OpeningTrainer(OpeningLibrary.ById("ruy_lopez")!);
        Assert.False(trainer.Play("a2a3")); // legal but not the book move
        Assert.Equal("e2e4", trainer.ExpectedMove); // still waiting on the first book move
    }

    // --- Bot roster ---

    [Fact]
    public void Bot_roster_has_profiles_within_engine_strength_range()
    {
        Assert.NotEmpty(BotRoster.All);
        Assert.Equal(1800, BotRoster.ById("club")!.Elo);
        Assert.Null(BotRoster.ById("nope"));
        Assert.All(BotRoster.All, b => Assert.InRange(b.Elo, 1320, 3190));
    }

    // --- Rating-range / custom set drilling ---

    [Fact]
    public void Rating_range_filter_returns_only_in_range_puzzles()
    {
        var library = ScenarioLibrary.LoadDefault();
        var set = library.ByRatingRange(700, 1000);

        Assert.NotEmpty(set);
        Assert.All(set, s => Assert.InRange(s.Rating, 700, 1000));
    }

    [Fact]
    public void Puzzle_set_session_drills_the_given_set()
    {
        var library = ScenarioLibrary.LoadDefault();
        var session = new PuzzleSetSession(library.ByRatingRange(600, 1100), startRating: 800);

        var scenario = session.Next();
        var result = session.Submit(scenario.Solutions[0], TimeSpan.FromSeconds(3));

        Assert.True(result.Correct);
        Assert.Equal(1, session.Score);
    }
}
