namespace Kaissa.Training;

/// <summary>
/// A named computer opponent with a persona. A Stockfish bot has a fixed Elo cap; a Maia bot instead
/// names a Maia network file (human-like play at that rating band, run on lc0). <see cref="Archetype"/>
/// is a playing-style label (Hunter / Guardian / Savage / Observer / Mediator) used to give the roster
/// distinct characters, as the popular bot playgrounds do.
/// </summary>
public sealed record BotProfile(string Id, string Name, int Elo, string Style, string? Weights = null, string Archetype = "Mediator");

/// <summary>Opponents to play against, in addition to the adaptive one: Stockfish bots and Maia humans,
/// each given a character and a style. <see cref="Ladder"/> orders them by rating for the bot ladder.</summary>
public static class BotRoster
{
    // Stockfish bots. Elo values sit within the engine's supported strength-limit range. Ids and Elos are
    // load-bearing (saved data, tests); the display name is the persona.
    public static IReadOnlyList<BotProfile> All { get; } = new[]
    {
        new BotProfile("rookie", "Milo", 1350, "Loose and forgiving - a friendly first game.", Archetype: "Mediator"),
        new BotProfile("casual", "Rosa", 1500, "Plays sensibly, but misses tactics.", Archetype: "Observer"),
        new BotProfile("club", "Viktor", 1800, "Solid club strength; grinds you down.", Archetype: "Guardian"),
        new BotProfile("expert", "Nadia", 2100, "Sharp and accurate; punishes loose play.", Archetype: "Hunter"),
        new BotProfile("master", "Kane", 2500, "Punishing - complications everywhere.", Archetype: "Savage"),
    };

    // Maia human-like bots (neural nets trained on human games), each backed by a weights file.
    public static IReadOnlyList<BotProfile> Maia { get; } = new[]
    {
        new BotProfile("maia1100", "Pip", 1100, "Human beginner - natural, forgiving moves.", "maia-1100.pb.gz", "Mediator"),
        new BotProfile("maia1300", "Dora", 1300, "Human casual - quiet, practical play.", "maia-1300.pb.gz", "Observer"),
        new BotProfile("maia1500", "Theo", 1500, "Human intermediate - well-rounded.", "maia-1500.pb.gz", "Mediator"),
        new BotProfile("maia1700", "Vera", 1700, "Human strong-club - looks for the attack.", "maia-1700.pb.gz", "Hunter"),
        new BotProfile("maia1900", "Kato", 1900, "Human expert - sharp and enterprising.", "maia-1900.pb.gz", "Savage"),
    };

    /// <summary>All bots ordered by rating - the rungs of the bot ladder (weakest first).</summary>
    public static IReadOnlyList<BotProfile> Ladder { get; } =
        All.Concat(Maia).OrderBy(b => b.Elo).ThenBy(b => b.Weights == null ? 0 : 1).ToList();

    public static BotProfile? ById(string id) =>
        All.Concat(Maia).FirstOrDefault(b => b.Id == id);
}
