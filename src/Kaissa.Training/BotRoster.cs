namespace Kaissa.Training;

/// <summary>
/// A named computer opponent. A Stockfish bot has a fixed Elo cap; a Maia bot instead names a Maia
/// network file (human-like play at that rating band, run on lc0).
/// </summary>
public sealed record BotProfile(string Id, string Name, int Elo, string Style, string? Weights = null);

/// <summary>Opponents to play against, in addition to the adaptive one: Stockfish bots and Maia humans.</summary>
public static class BotRoster
{
    // Stockfish bots. Elo values sit within the engine's supported strength-limit range.
    public static IReadOnlyList<BotProfile> All { get; } = new[]
    {
        new BotProfile("rookie", "Rookie", 1350, "Loose and forgiving."),
        new BotProfile("casual", "Casual", 1500, "Plays sensibly, misses tactics."),
        new BotProfile("club", "Club Player", 1800, "Solid club strength."),
        new BotProfile("expert", "Expert", 2100, "Sharp and accurate."),
        new BotProfile("master", "Master", 2500, "Punishing."),
    };

    // Maia human-like bots (neural nets trained on human games), each backed by a weights file.
    public static IReadOnlyList<BotProfile> Maia { get; } = new[]
    {
        new BotProfile("maia1100", "Maia 1100", 1100, "Human - beginner.", "maia-1100.pb.gz"),
        new BotProfile("maia1300", "Maia 1300", 1300, "Human - casual.", "maia-1300.pb.gz"),
        new BotProfile("maia1500", "Maia 1500", 1500, "Human - intermediate.", "maia-1500.pb.gz"),
        new BotProfile("maia1700", "Maia 1700", 1700, "Human - strong club.", "maia-1700.pb.gz"),
        new BotProfile("maia1900", "Maia 1900", 1900, "Human - expert.", "maia-1900.pb.gz"),
    };

    public static BotProfile? ById(string id) =>
        All.Concat(Maia).FirstOrDefault(b => b.Id == id);
}
