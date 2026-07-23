namespace Kaissa.Training;

/// <summary>
/// An expedition: a short campaign to master one opening by playing it repeatedly against a bot until
/// you win enough games (Lucas Chess's "expeditions", ChessBase's "play this line vs the engine").
/// Which side you take is fixed - "beat the Sicilian" is played as White, "hold the Caro-Kann" as
/// Black. Keyed to <see cref="OpeningLibrary"/> ids so the client can load the line.
/// </summary>
public sealed record Expedition(string Id, string Title, string OpeningId, string Description, int TargetWins, bool PlayAsWhite);

/// <summary>The catalogue of built-in expeditions, one per opening in the library.</summary>
public static class Expeditions
{
    public static IReadOnlyList<Expedition> Catalog { get; } = new[]
    {
        new Expedition("exp_italian", "Master the Italian", "italian",
            "Win three games from the Italian as White.", 3, true),
        new Expedition("exp_ruy_lopez", "Master the Ruy Lopez", "ruy_lopez",
            "Win three games from the Ruy Lopez as White.", 3, true),
        new Expedition("exp_sicilian", "Beat the Sicilian", "sicilian",
            "Win three games against the Sicilian as White.", 3, true),
        new Expedition("exp_queens_gambit", "Master the Queen's Gambit", "queens_gambit",
            "Win three games from the Queen's Gambit as White.", 3, true),
        new Expedition("exp_london", "Master the London", "london",
            "Win three games from the London System as White.", 3, true),
    };

    public static Expedition? ById(string id) => Catalog.FirstOrDefault(e => e.Id == id);
}

/// <summary>
/// Tracks progress through one expedition: games won and lost, and whether the win target is met.
/// Pure state (no clock, no engine) so it serialises and tests cleanly.
/// </summary>
public sealed class ExpeditionRun
{
    private readonly int _target;

    public ExpeditionRun(Expedition expedition, int wins = 0, int losses = 0)
    {
        if (expedition is null) throw new ArgumentNullException(nameof(expedition));
        _target = Math.Max(1, expedition.TargetWins);
        ExpeditionId = expedition.Id;
        Wins = Math.Max(0, wins);
        Losses = Math.Max(0, losses);
    }

    public string ExpeditionId { get; }
    public int Wins { get; private set; }
    public int Losses { get; private set; }
    public int GamesPlayed => Wins + Losses;
    public int TargetWins => _target;

    public bool IsComplete => Wins >= _target;

    /// <summary>Fraction of the win target reached, clamped to 0..1.</summary>
    public double Progress => Math.Min(1.0, (double)Wins / _target);

    /// <summary>Record a finished game. Wins past the target are ignored (the expedition is done).</summary>
    public void Record(bool won)
    {
        if (IsComplete) return;
        if (won) Wins++; else Losses++;
    }
}
