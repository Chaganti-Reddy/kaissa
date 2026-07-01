using Kaissa.Learning;

namespace Kaissa.Training;

/// <summary>What happened after the player submitted a move.</summary>
public sealed record SubmitOutcome(
    Scenario Scenario,
    bool Correct,
    Rating Rating,
    int IntervalDays,
    DateTime DueUtc,
    double Stability);

/// <summary>
/// The headless training loop: pick a scenario, take the player's move, grade it, update the
/// schedule, repeat. It ties the three cores together — rules (move checking), learning (FSRS),
/// and content — with no UI and no engine dependency, so the loop can be proven before any 3D
/// work. Presentation layers drive this class; they do not reimplement it.
/// </summary>
public sealed class TrainingSession
{
    private readonly ScenarioLibrary _library;
    private readonly SkillModel _model;
    private readonly FsrsScheduler _scheduler;
    private readonly SessionPlanner _planner;
    private readonly GradeExtractor _grader;
    private readonly IClock _clock;
    private readonly Dictionary<PatternId, int> _rotation = new();

    private Scenario? _current;

    public TrainingSession(
        ScenarioLibrary library,
        SkillModel model,
        IClock clock,
        FsrsScheduler? scheduler = null,
        SessionPlanner? planner = null,
        GradeExtractor? grader = null)
    {
        _library = library;
        _model = model;
        _clock = clock;
        _scheduler = scheduler ?? new FsrsScheduler();
        _planner = planner ?? new SessionPlanner();
        _grader = grader ?? new GradeExtractor();
    }

    /// <summary>Selects the next scenario to present, or null if there is no content.</summary>
    public Scenario? Next()
    {
        var pattern = _planner.NextPattern(_model, _library, _clock.UtcNow);
        if (pattern is not { } id)
            return null;

        var scenarios = _library.ForPattern(id);
        if (scenarios.Count == 0)
            return null;

        // Rotate through a pattern's scenarios so the same idea is drilled from different positions.
        var index = _rotation.GetValueOrDefault(id);
        _rotation[id] = index + 1;
        _current = scenarios[index % scenarios.Count];
        return _current;
    }

    /// <summary>Grades the player's move for the current scenario and updates the schedule.</summary>
    public SubmitOutcome Submit(string move, TimeSpan thinkingTime)
    {
        if (_current is null)
            throw new InvalidOperationException("Call Next() before Submit().");

        var scenario = _current;
        var attempt = _grader.Grade(scenario, move, thinkingTime);

        var card = _model.GetOrCreate(scenario.Pattern);
        var now = _clock.UtcNow;
        double elapsedDays = card.LastReviewUtc is { } last ? (now - last).TotalDays : 0;

        var review = _scheduler.Review(card.State, elapsedDays, attempt.Rating);

        card.State = review.State;
        card.LastReviewUtc = now;
        card.DueUtc = now.AddDays(review.IntervalDays);
        card.Reps++;
        if (attempt.Rating == Rating.Again)
            card.Lapses++;

        _current = null;

        return new SubmitOutcome(
            scenario, attempt.Correct, attempt.Rating, review.IntervalDays, card.DueUtc.Value, review.State.Stability);
    }
}
