namespace Kaissa.Training;

/// <summary>
/// A single training position: a FEN, the pattern it trains, the accepted solution move(s) in
/// UCI, a prompt to show the player, and a difficulty <see cref="Rating"/> (Elo-scale). Content is
/// data, not code, so it can be authored and (later) generated without changing the app.
/// </summary>
public sealed record Scenario(
    string Id,
    PatternId Pattern,
    string Fen,
    IReadOnlyList<string> Solutions,
    string Prompt,
    int Rating);
