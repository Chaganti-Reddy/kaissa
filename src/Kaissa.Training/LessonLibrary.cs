using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>A guided lesson: an idea explained in our own words, then practised on real positions.</summary>
public sealed record Lesson(string Id, string Topic, string Title, string Intro, PatternId Pattern, int Challenges, string Level = "Beginner");

/// <summary>One screen of a lesson: an explanation over a position, optionally asking for a move.</summary>
public sealed record LessonStep(
    string Fen, string Text, bool Interactive, string? ExpectedMove, string SuccessText, bool WhiteBottom, int Index, int Total);

/// <summary>
/// The lesson catalogue. Lessons teach a concept in our own words and then drill it on real positions
/// pulled from the puzzle library by pattern, so every challenge is a legal, solvable example without
/// authoring FENs by hand. Content and wording are our own.
/// </summary>
public static class LessonLibrary
{
    public static IReadOnlyList<Lesson> All { get; } = new[]
    {
        new Lesson("fork", "Tactics", "The fork",
            "A fork is one piece attacking two or more targets at once. The opponent can only save one, so you win the other. Knights fork especially well because they hit squares no other piece defends. Look for a move that attacks the king or queen and a second piece together.",
            new PatternId("tactic.fork"), 4),
        new Lesson("pin", "Tactics", "The pin",
            "A pin freezes a piece: it cannot move without exposing a more valuable piece behind it. Pin against the king and the piece is absolutely stuck. Once a piece is pinned, pile more attackers on it - it cannot run.",
            new PatternId("tactic.pin"), 4),
        new Lesson("skewer", "Tactics", "The skewer",
            "A skewer is a pin turned around: a valuable piece is attacked and, when it moves, a lesser piece behind it falls. Line your rook, bishop, or queen up against the enemy king or queen with something worth taking behind it.",
            new PatternId("tactic.skewer"), 4, "Intermediate"),
        new Lesson("discovered", "Tactics", "Discovered attack",
            "Moving one piece can unveil an attack from another behind it. The moving piece is free to do its own damage while the discovered attacker does the real work. The strongest form is a discovered check, because the opponent must answer the check first.",
            new PatternId("tactic.discovered_attack"), 4, "Intermediate"),
        new Lesson("deflection", "Tactics", "Deflection",
            "A defender can only guard so much. Deflection forces a defending piece away from its job - often with a capture or threat it must answer - so the square or piece it was protecting falls.",
            new PatternId("tactic.deflection"), 4, "Advanced"),

        new Lesson("backrank", "Checkmates", "Back-rank mate",
            "A king castled behind its own pawns can be trapped on the back rank. A rook or queen delivered along that rank is mate when the king has no escape squares. Watch for the moment the defenders are gone.",
            new PatternId("checkmate.back_rank"), 4),
        new Lesson("smothered", "Checkmates", "Smothered mate",
            "When a king is boxed in by its own pieces, a knight can deliver a mate nothing can block or capture. The classic pattern uses a queen sacrifice to force the king into the corner first.",
            new PatternId("checkmate.smothered"), 3, "Advanced"),
        new Lesson("matein2", "Checkmates", "Mate in two",
            "Forcing mate in two moves means finding a first move - often a check or a quiet threat - that leaves the opponent no defence against mate next move. Calculate the opponent's only replies, then the finish.",
            new PatternId("checkmate.mate_in_two"), 4, "Intermediate"),
    };

    public static IReadOnlyList<string> Topics { get; } = All.Select(l => l.Topic).Distinct().ToList();

    public static IReadOnlyList<Lesson> ByTopic(string topic) => All.Where(l => l.Topic == topic).ToList();

    /// <summary>Skill levels in ladder order (chess.com-style grouping), only those that have lessons.</summary>
    public static IReadOnlyList<string> Levels { get; } =
        new[] { "Beginner", "Intermediate", "Advanced" }.Where(v => All.Any(l => l.Level == v)).ToList();

    public static IReadOnlyList<Lesson> ByLevel(string level) => All.Where(l => l.Level == level).ToList();

    public static Lesson? ById(string id) => All.FirstOrDefault(l => l.Id == id);
}

/// <summary>
/// Runs one lesson: an intro step (explanation over a sample position) followed by the lesson's
/// challenges (solve the pattern on real positions from the library). Pure logic, unit-testable.
/// </summary>
public sealed class LessonSession
{
    private readonly List<LessonStep> _steps = new();

    public LessonSession(Lesson lesson, ScenarioLibrary library, int? seed = null)
    {
        var pool = library.ForPattern(lesson.Pattern).ToList();
        if (seed is { } s) Shuffle(pool, s);

        // Reserve pool[0] as the intro's illustrative position, then draw challenges from pool[1..] so the
        // position the player is asked to solve is never the same one the intro just walked through.
        int want = Math.Min(lesson.Challenges, Math.Max(0, pool.Count - 1));

        int total = want + 1; // intro + challenges
        if (pool.Count > 0)
            _steps.Add(new LessonStep(pool[0].Fen, lesson.Intro, false, null,
                "", WhiteToMove(pool[0].Fen), 0, total));

        for (int i = 0; i < want; i++)
        {
            var sc = pool[i + 1];
            _steps.Add(new LessonStep(sc.Fen, "Your turn - find it.", true, sc.Solutions.Count > 0 ? sc.Solutions[0] : null,
                "Correct.", WhiteToMove(sc.Fen), i + 1, total));
        }
    }

    public IReadOnlyList<LessonStep> Steps => _steps;
    public int Count => _steps.Count;
    public LessonStep this[int i] => _steps[i];

    private static bool WhiteToMove(string fen)
    {
        var p = fen.Split(' ');
        return p.Length < 2 || p[1] != "b";
    }

    private static void Shuffle(List<Scenario> list, int seed)
    {
        var rng = new Random(seed);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
