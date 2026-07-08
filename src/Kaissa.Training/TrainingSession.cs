using Kaissa.Learning;

namespace Kaissa.Training;

/// <summary>What happened after the player submitted a move.</summary>
public sealed record SubmitOutcome(
    Scenario Scenario,
    bool Correct,
    Rating Rating,
    int IntervalDays,
    DateTime DueUtc,
    double Stability,
    double PlayerRating);

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
    private readonly DifficultyController _difficulty;
    private readonly IClock _clock;
    private readonly Dictionary<PatternId, string> _lastShown = new();

    private Scenario? _current;

    public TrainingSession(
        ScenarioLibrary library,
        SkillModel model,
        IClock clock,
        FsrsScheduler? scheduler = null,
        SessionPlanner? planner = null,
        GradeExtractor? grader = null,
        DifficultyController? difficulty = null)
    {
        _library = library;
        _model = model;
        _clock = clock;
        _scheduler = scheduler ?? new FsrsScheduler();
        _planner = planner ?? new SessionPlanner();
        _grader = grader ?? new GradeExtractor();
        _difficulty = difficulty ?? new DifficultyController();
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

        // Pick a position near the player's level on THIS pattern, avoiding an immediate repeat.
        _current = _difficulty.Pick(scenarios, _model.PatternRating(id), _lastShown.GetValueOrDefault(id));
        _lastShown[id] = _current.Id;
        return _current;
    }

    /// <summary>
    /// Grades the player's move for the current scenario and updates the schedule. If the answer was
    /// assisted (a hint was used) it is treated as a lapse and earns no rating credit, so the pattern
    /// is resurfaced soon — a hinted answer is not genuine recall.
    /// </summary>
    public SubmitOutcome Submit(string move, TimeSpan thinkingTime, bool assisted = false)
    {
        if (_current is null)
            throw new InvalidOperationException("Call Next() before Submit().");

        var scenario = _current;
        var attempt = _grader.Grade(scenario, move, thinkingTime);

        bool correct = attempt.Correct && !assisted;
        var rating = assisted ? Rating.Again : attempt.Rating;

        var card = _model.GetOrCreate(scenario.Pattern);
        if (!card.Seen)
            card.Rating = _model.RatingEstimate; // seed a new pattern from the player's overall level
        var now = _clock.UtcNow;
        double elapsedDays = card.LastReviewUtc is { } last ? (now - last).TotalDays : 0;

        var review = _scheduler.Review(card.State, elapsedDays, rating);

        card.State = review.State;
        card.LastReviewUtc = now;
        card.DueUtc = now.AddDays(review.IntervalDays);
        card.Reps++;
        if (rating == Rating.Again)
            card.Lapses++;

        // Update this pattern's own rating, and the overall estimate (kept as an aggregate for
        // display and for seeding the play-vs-bot strength).
        card.Rating = RatingEstimator.Update(card.Rating, scenario.Rating, correct);
        _model.RatingEstimate = RatingEstimator.Update(_model.RatingEstimate, scenario.Rating, correct);
        _model.RecordResult(correct, _model.RatingEstimate);

        _current = null;

        return new SubmitOutcome(
            scenario, correct, rating, review.IntervalDays, card.DueUtc.Value,
            review.State.Stability, _model.RatingEstimate);
    }
}
