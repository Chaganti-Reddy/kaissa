namespace Kaissa.Training;

/// <summary>The outcome of one Puzzle Rush attempt.</summary>
public readonly record struct RushResult(
    bool Correct,
    int Score,
    int Lives,
    int Streak,
    bool IsOver,
    IReadOnlyList<string> Solutions);

/// <summary>
/// Puzzle Rush: solve as many puzzles as possible before running out of lives. Difficulty ramps
/// up with each solve, so it keeps pushing the player's edge. Unlike the training loop this is an
/// arcade mode - no spaced scheduling - but it reuses the same content and grading. A client adds
/// the timer; the core tracks score, streak, and lives.
/// </summary>
public sealed class RushSession
{
    private const double RampPerSolve = 25.0;

    private readonly ScenarioLibrary _library;
    private readonly GradeExtractor _grader;
    private readonly HashSet<string> _used = new();

    private double _targetRating;
    private Scenario? _current;

    public RushSession(ScenarioLibrary library, double startRating = 800, int lives = 3, GradeExtractor? grader = null)
    {
        _library = library;
        _grader = grader ?? new GradeExtractor();
        _targetRating = startRating;
        Lives = lives;
    }

    public static RushSession CreateDefault(double startRating = 800, int lives = 3) =>
        new(ScenarioLibrary.LoadDefault(), startRating, lives);

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Streak { get; private set; }
    public bool IsOver => Lives <= 0;
    public double TargetRating => _targetRating;

    /// <summary>The next puzzle to present: the one nearest the current (rising) target rating.</summary>
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

    /// <summary>The from-square of the current puzzle's solution, as a hint (null if none).</summary>
    public string? Hint()
    {
        if (_current is null || _current.Solutions.Count == 0)
            return null;
        var uci = _current.Solutions[0];
        return uci.Length >= 2 ? uci.Substring(0, 2) : null;
    }

    /// <summary>
    /// Grades the answer: a solve raises the score and difficulty; a miss costs a life. If a hint was
    /// used the answer earns no score and breaks the streak, but does not cost a life.
    /// </summary>
    public RushResult Submit(string move, TimeSpan thinkingTime, bool assisted = false)
    {
        if (_current is null)
            throw new InvalidOperationException("Call Next() before Submit().");
        if (IsOver)
            return new RushResult(false, Score, Lives, Streak, true, _current.Solutions);

        var attempt = _grader.Grade(_current, move, thinkingTime);
        bool correct = attempt.Correct && !assisted;
        if (assisted)
        {
            Streak = 0; // hinted: no credit, but not a life lost
        }
        else if (attempt.Correct)
        {
            Score++;
            Streak++;
            _targetRating += RampPerSolve;
        }
        else
        {
            Lives--;
            Streak = 0;
        }

        var solutions = _current.Solutions;
        _current = null;
        return new RushResult(correct, Score, Lives, Streak, IsOver, solutions);
    }
}
