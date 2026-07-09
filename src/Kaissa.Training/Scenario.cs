namespace Kaissa.Training;

/// <summary>
/// A single training position: a FEN, the pattern it trains, the accepted solution move(s) in
/// UCI, a prompt to show the player, and a difficulty <see cref="Rating"/> (Elo-scale). Content is
/// data, not code, so it can be authored and (later) generated without changing the app.
///
/// Multi-move puzzles carry the full <see cref="Line"/> from the solver's first turn onward, in
/// UCI: solver move, opponent reply, solver move, ... The player must find every solver move; the
/// opponent's replies are auto-played. <see cref="Solutions"/> stays the accepted first move(s) so
/// existing single-move grading and older content keep working. <see cref="Setup"/> is the
/// opponent move that was applied to reach <see cref="Fen"/> (for the load animation), if any.
/// </summary>
public sealed record Scenario(
    string Id,
    PatternId Pattern,
    string Fen,
    IReadOnlyList<string> Solutions,
    string Prompt,
    int Rating,
    IReadOnlyList<string>? Line = null,
    IReadOnlyList<string>? Themes = null,
    string? Setup = null)
{
    /// <summary>The full solver/opponent sequence from <see cref="Fen"/>. Falls back to the first
    /// accepted solution for single-move content that has no explicit line.</summary>
    public IReadOnlyList<string> SolverLine =>
        Line is { Count: > 0 } ? Line : Solutions;

    /// <summary>True when the puzzle is a single solver move (no opponent replies to play out).</summary>
    public bool IsSingleMove => SolverLine.Count <= 1;

    public IReadOnlyList<string> ThemeTags => Themes ?? Array.Empty<string>();
}
