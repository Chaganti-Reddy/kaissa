using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class BlitzRatingTests
{
    [Fact]
    public void Blitz_rating_moves_up_on_a_solve_and_down_on_a_miss()
    {
        var m = new SkillModel();
        double start = m.BlitzRating;
        m.UpdateBlitz(1500, solved: true);
        Assert.True(m.BlitzRating > start);

        double afterWin = m.BlitzRating;
        m.UpdateBlitz(1500, solved: false);
        Assert.True(m.BlitzRating < afterWin);
    }

    [Fact]
    public void Blitz_rating_is_independent_of_the_standard_rating()
    {
        var m = new SkillModel();
        m.UpdateBlitz(2000, solved: true);
        Assert.NotEqual(m.RatingEstimate, m.BlitzRating); // updating blitz did not move the standard rating
    }

    [Fact]
    public void Blitz_rating_survives_a_json_round_trip()
    {
        var m = new SkillModel();
        m.UpdateBlitz(1800, solved: true);
        var reloaded = SkillModel.FromJson(m.ToJson());
        Assert.Equal(m.BlitzRating, reloaded.BlitzRating, 3);
    }

    [Fact]
    public void Old_saves_without_a_blitz_rating_fall_back_to_the_overall_rating()
    {
        // A save from before the blitz rating existed - only RatingEstimate present.
        var reloaded = SkillModel.FromJson("{\"RatingEstimate\":1234}");
        Assert.Equal(1234, reloaded.BlitzRating, 3);
    }
}
