namespace Kaissa.Training;

/// <summary>
/// Identifies a single learnable chess pattern (e.g. "checkmate.back_rank"). Patterns are the
/// unit the scheduler tracks; see docs/learning-engine.md.
/// </summary>
public readonly record struct PatternId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>A learnable pattern and its human-readable description.</summary>
public sealed record Pattern(PatternId Id, string Name, string Description);
