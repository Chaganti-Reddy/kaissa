namespace Kaissa.Training;

/// <summary>
/// First-run calibration: a short run of puzzles that adapts to the player's answers to estimate a
/// starting rating, so difficulty fits from the very first real session instead of everyone
/// beginning at the same default.
/// </summary>
public sealed class CalibrationSession
{
    private readonly IReadOnlyList<Scenario> _all;
    private readonly GradeExtractor _grader;
    private readonly DifficultyController _difficulty;
    private readonly int _count;

    private double _rating;
    private string? _lastShown;
    private Scenario? _current;

    public CalibrationSession(ScenarioLibrary library, int puzzles = 12, double startRating = 1200,
        GradeExtractor? grader = null, DifficultyController? difficulty = null)
    {
        _all = library.AllScenarios.ToList();
        if (_all.Count == 0)
            throw new InvalidOperationException("No scenarios to calibrate with.");
        _count = puzzles;
        _rating = startRating;
        _grader = grader ?? new GradeExtractor();
        _difficulty = difficulty ?? new DifficultyController(targetOffset: 0);
    }

    public bool IsComplete => Answered >= _count;
    public int Answered { get; private set; }
    public int Total => _count;
    public double EstimatedRating => _rating;

    public Scenario? Next()
    {
        if (IsComplete)
            return null;
        _current = _difficulty.Pick(_all, _rating, _lastShown);
        _lastShown = _current.Id;
        return _current;
    }

    /// <summary>Grades the answer and updates the estimate; returns the running rating.</summary>
    public double Submit(string move, TimeSpan thinkingTime)
    {
        if (_current is null)
            throw new InvalidOperationException("Call Next() before Submit().");

        var attempt = _grader.Grade(_current, move, thinkingTime);

        // A large step that decays over the run: converges fast early, settles by the end.
        double expected = RatingEstimator.ExpectedScore(_rating, _current.Rating);
        double actual = attempt.Correct ? 1.0 : 0.0;
        double k = Math.Max(30.0, 120.0 - Answered * 7.0);
        _rating = Math.Clamp(_rating + k * (actual - expected), 100.0, 3000.0);

        Answered++;
        _current = null;
        return _rating;
    }
}
