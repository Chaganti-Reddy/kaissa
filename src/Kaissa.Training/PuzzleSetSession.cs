namespace Kaissa.Training;

/// <summary>
/// Drills an arbitrary set of puzzles, difficulty-matched to a running rating. Powers rating-range
/// practice, custom sets, and the weakness-report practice set — anything that is "here is a list
/// of puzzles, work through them" without spaced scheduling.
/// </summary>
public sealed class PuzzleSetSession
{
    private readonly IReadOnlyList<Scenario> _set;
    private readonly GradeExtractor _grader;
    private readonly DifficultyController _difficulty;

    private double _rating;
    private string? _lastShown;
    private Scenario? _current;

    public PuzzleSetSession(IReadOnlyList<Scenario> set, double startRating = 800,
        GradeExtractor? grader = null, DifficultyController? difficulty = null)
    {
        if (set.Count == 0)
            throw new ArgumentException("Puzzle set is empty.", nameof(set));
        _set = set;
        _grader = grader ?? new GradeExtractor();
        _difficulty = difficulty ?? new DifficultyController();
        _rating = startRating;
    }

    public int Score { get; private set; }
    public int Attempts { get; private set; }
    public double Rating => _rating;
    public int Count => _set.Count;

    public Scenario Next()
    {
        _current = _difficulty.Pick(_set, _rating, _lastShown);
        _lastShown = _current.Id;
        return _current;
    }

    public ThemedResult Submit(string move, TimeSpan thinkingTime)
    {
        if (_current is null)
            throw new InvalidOperationException("Call Next() before Submit().");

        var attempt = _grader.Grade(_current, move, thinkingTime);
        Attempts++;
        if (attempt.Correct)
            Score++;
        _rating = RatingEstimator.Update(_rating, _current.Rating, attempt.Correct);

        var solutions = _current.Solutions;
        _current = null;
        return new ThemedResult(attempt.Correct, solutions, Score, Attempts, _rating);
    }
}
