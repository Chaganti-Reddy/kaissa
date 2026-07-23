namespace Kaissa.Training;

/// <summary>The outcome of one Puzzle Streak answer.</summary>
public readonly record struct StreakResult(
    bool Correct,
    int Score,
    int Best,
    bool SkipUsed,
    bool IsOver,
    IReadOnlyList<string> Solutions);

/// <summary>
/// Puzzle Streak (Lichess model): no clock, puzzles get progressively harder, a single wrong answer
/// ends the run, and the player gets exactly one skip for the whole streak. Score is the count solved.
/// Reuses the same content and grading as the training loop; the difficulty ramp mirrors Puzzle Rush
/// but without lives - the first miss is terminal. A client shows the position and the running score.
/// </summary>
public sealed class StreakSession
{
    private const double RampPerSolve = 30.0;

    private readonly ScenarioLibrary _library;
    private readonly GradeExtractor _grader;
    private readonly HashSet<string> _used = new();

    private double _targetRating;
    private Scenario? _current;

    public StreakSession(ScenarioLibrary library, double startRating = 800, GradeExtractor? grader = null)
    {
        _library = library;
        _grader = grader ?? new GradeExtractor();
        _targetRating = startRating;
    }

    public static StreakSession CreateDefault(double startRating = 800) =>
        new(ScenarioLibrary.LoadDefault(), startRating);

    public int Score { get; private set; }
    public int Best { get; private set; }
    public bool SkipUsed { get; private set; }
    public bool IsOver { get; private set; }
    public double TargetRating => _targetRating;

    /// <summary>The next puzzle: the one nearest the current (rising) target rating.</summary>
    public Scenario? Next()
    {
        if (IsOver)
            return null;

        var pool = _library.AllScenarios.Where(s => !_used.Contains(s.Id)).ToList();
        if (pool.Count == 0)
        {
            _used.Clear();
            pool = _library.AllScenarios.ToList();
        }

        _current = pool
            .OrderBy(s => Math.Abs(s.Rating - _targetRating))
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .First();
        _used.Add(_current.Id);
        return _current;
    }

    /// <summary>
    /// Skip the current puzzle without ending the streak. Allowed once per run; a second call is a
    /// no-op that returns false. Does not change the score. Call <see cref="Next"/> afterwards.
    /// </summary>
    public bool Skip()
    {
        if (IsOver || SkipUsed || _current is null)
            return false;
        SkipUsed = true;
        _current = null;
        return true;
    }

    /// <summary>Grades the answer: a solve raises the score and difficulty; the first miss ends the run.</summary>
    public StreakResult Submit(string move, TimeSpan thinkingTime)
    {
        if (_current is null)
            throw new InvalidOperationException("Call Next() before Submit().");
        if (IsOver)
            return new StreakResult(false, Score, Best, SkipUsed, true, _current.Solutions);

        var attempt = _grader.Grade(_current, move, thinkingTime);
        if (attempt.Correct)
        {
            Score++;
            if (Score > Best) Best = Score;
            _targetRating += RampPerSolve;
        }
        else
        {
            IsOver = true;
        }

        var solutions = _current.Solutions;
        _current = null;
        return new StreakResult(attempt.Correct, Score, Best, SkipUsed, IsOver, solutions);
    }
}
