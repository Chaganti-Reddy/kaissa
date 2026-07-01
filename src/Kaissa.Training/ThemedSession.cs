namespace Kaissa.Training;

/// <summary>Result of one themed-practice attempt.</summary>
public readonly record struct ThemedResult(
    bool Correct,
    IReadOnlyList<string> Solutions,
    int Score,
    int Attempts,
    double Rating);

/// <summary>
/// Drill a single pattern on demand ("practice forks"). No spaced scheduling — it just serves that
/// pattern's positions, difficulty-matched to a running rating. Complements the adaptive loop for
/// players who want to grind a specific idea.
/// </summary>
public sealed class ThemedSession
{
    private readonly IReadOnlyList<Scenario> _scenarios;
    private readonly GradeExtractor _grader;
    private readonly DifficultyController _difficulty;

    private double _rating;
    private string? _lastShown;
    private Scenario? _current;

    public ThemedSession(ScenarioLibrary library, PatternId pattern, double startRating = 800,
        GradeExtractor? grader = null, DifficultyController? difficulty = null)
    {
        Pattern = pattern;
        _scenarios = library.ForPattern(pattern);
        if (_scenarios.Count == 0)
            throw new ArgumentException($"No scenarios for pattern '{pattern}'.", nameof(pattern));
        _grader = grader ?? new GradeExtractor();
        _difficulty = difficulty ?? new DifficultyController();
        _rating = startRating;
    }

    public PatternId Pattern { get; }
    public int Score { get; private set; }
    public int Attempts { get; private set; }
    public double Rating => _rating;

    public Scenario Next()
    {
        _current = _difficulty.Pick(_scenarios, _rating, _lastShown);
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
