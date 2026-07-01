namespace Kaissa.Training;

/// <summary>
/// A single training position: a FEN, the pattern it trains, the accepted solution move(s) in
/// UCI, and a prompt to show the player. Content is data, not code, so it can be authored and
/// (later) generated without changing the app.
/// </summary>
public sealed record Scenario(
    string Id,
    PatternId Pattern,
    string Fen,
    IReadOnlyList<string> Solutions,
    string Prompt);
