namespace Kaissa.Training;

/// <summary>A named computer opponent at a fixed strength.</summary>
public sealed record BotProfile(string Id, string Name, int Elo, string Style);

/// <summary>A ladder of fixed-strength bots to play against, in addition to the adaptive opponent.</summary>
public static class BotRoster
{
    // Elo values sit within the engine's supported strength-limit range.
    public static IReadOnlyList<BotProfile> All { get; } = new[]
    {
        new BotProfile("rookie", "Rookie", 1350, "Loose and forgiving."),
        new BotProfile("casual", "Casual", 1500, "Plays sensibly, misses tactics."),
        new BotProfile("club", "Club Player", 1800, "Solid club strength."),
        new BotProfile("expert", "Expert", 2100, "Sharp and accurate."),
        new BotProfile("master", "Master", 2500, "Punishing."),
    };

    public static BotProfile? ById(string id) => All.FirstOrDefault(b => b.Id == id);
}
