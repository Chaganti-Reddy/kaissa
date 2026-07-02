namespace Kaissa.Training.Api;

/// <summary>
/// The single entry point a UI (e.g. the Unity client) uses to run the puzzle-training loop. It
/// wraps the library, skill model, and session, and speaks only in plain DTOs — no engine or rules
/// types leak across this boundary. Persistence is the caller's job via <see cref="ExportProgress"/>.
/// </summary>
public sealed class KaissaTrainer
{
    private readonly ScenarioLibrary _library;
    private readonly SkillModel _model;
    private readonly TrainingSession _session;

    private Scenario? _current;

    public KaissaTrainer(ScenarioLibrary library, SkillModel model, IClock? clock = null)
    {
        _library = library;
        _model = model;
        _session = new TrainingSession(library, model, clock ?? new SystemClock());
    }

    /// <summary>Creates a trainer over the bundled content, resuming saved progress if provided.</summary>
    public static KaissaTrainer CreateDefault(string? savedProgressJson = null)
    {
        var model = savedProgressJson is null ? new SkillModel() : SkillModel.FromJson(savedProgressJson);
        return new KaissaTrainer(ScenarioLibrary.LoadDefault(), model);
    }

    public double PlayerRating => _model.RatingEstimate;

    /// <summary>Adds player-specific content (e.g. positions from their own games) to be scheduled.</summary>
    public void AddScenarios(Pattern pattern, IEnumerable<Scenario> scenarios) => _library.Add(pattern, scenarios);

    /// <summary>The next position to present, or null if there is no content.</summary>
    public TrainingCard? NextCard()
    {
        _current = _session.Next();
        if (_current is null)
            return null;

        var pattern = _library.Describe(_current.Pattern);
        return new TrainingCard(
            pattern.Id.Value,
            pattern.Name,
            pattern.Description,
            BoardView.FromFen(_current.Fen),
            _current.Prompt,
            _current.Rating,
            _model.RatingEstimate,
            Kaissa.Chess.Rules.ChessGame.FromFen(_current.Fen).LegalUciMoves());
    }

    /// <summary>Grades the player's move for the current card and advances the schedule.</summary>
    public AnswerResult Answer(string move, TimeSpan thinkingTime)
    {
        if (_current is null)
            throw new InvalidOperationException("Call NextCard() before Answer().");

        double before = _model.RatingEstimate;
        var solutions = _current.Solutions;
        var outcome = _session.Submit(move, thinkingTime);

        return new AnswerResult(
            outcome.Correct,
            outcome.Rating.ToString(),
            solutions,
            outcome.IntervalDays,
            outcome.PlayerRating,
            outcome.PlayerRating - before);
    }

    /// <summary>The player's progress across every pattern.</summary>
    public IReadOnlyList<ProgressRow> Progress()
    {
        var rows = new List<ProgressRow>();
        foreach (var patternId in _library.Patterns)
        {
            var pattern = _library.Describe(patternId);
            if (_model.Has(patternId))
            {
                var card = _model.GetOrCreate(patternId);
                rows.Add(new ProgressRow(patternId.Value, pattern.Name, true, card.Reps, card.Lapses,
                    card.State?.Stability ?? 0));
            }
            else
            {
                rows.Add(new ProgressRow(patternId.Value, pattern.Name, false, 0, 0, 0));
            }
        }

        return rows;
    }

    /// <summary>Headline stats for an insights screen.</summary>
    public PlayerStats GetStats()
    {
        int attempts = 0, correct = 0, seen = 0;
        foreach (var card in _model.Cards)
        {
            if (card.Seen)
                seen++;
            attempts += card.Reps;
            correct += card.Reps - card.Lapses;
        }

        double accuracy = attempts > 0 ? (double)correct / attempts : 0;
        return new PlayerStats(_model.RatingEstimate, attempts, correct, accuracy, seen,
            _model.CurrentStreak, _model.BestStreak, _model.RatingHistory.ToList());
    }

    /// <summary>Serialised progress for the caller to persist.</summary>
    public string ExportProgress() => _model.ToJson();
}
