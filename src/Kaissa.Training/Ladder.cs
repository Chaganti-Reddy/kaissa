namespace Kaissa.Training;

/// <summary>One rung of the bot ladder: the opponent, how many tutor hints you get here, and whether
/// it is unlocked / already beaten.</summary>
public sealed record LadderRung(int Index, BotProfile Bot, int HintsAllowed, bool Unlocked, bool Beaten);

/// <summary>
/// A ladder up the bot roster (weakest first): beat a rung to unlock the next, and the number of
/// tutor hints you get shrinks as you climb (Lucas Chess's model - more help low down, none at the
/// top). Progress is just the set of beaten bot ids, so it serialises trivially and is deterministic
/// for tests. The engine/opponent lives behind the UCI seam; this type only tracks progression.
/// </summary>
public sealed class LadderProgression
{
    private readonly IReadOnlyList<BotProfile> _bots;
    private readonly int _startHints;
    private readonly HashSet<string> _beaten;

    public LadderProgression(IReadOnlyList<BotProfile>? bots = null, int startHints = 5, IEnumerable<string>? beaten = null)
    {
        _bots = bots ?? BotRoster.Ladder;
        _startHints = Math.Max(0, startHints);
        _beaten = new HashSet<string>(beaten ?? Array.Empty<string>());
    }

    public int Count => _bots.Count;

    /// <summary>Beaten bot ids, for saving.</summary>
    public IReadOnlyCollection<string> BeatenIds => _beaten;

    /// <summary>Hints granted at a rung: full at the bottom, dropping by one per rung down to zero.</summary>
    public int HintsFor(int index) => Math.Max(0, _startHints - index);

    /// <summary>A rung is unlocked when it is the first, or the rung below it has been beaten.</summary>
    public bool IsUnlocked(int index) =>
        index <= 0 || (index - 1 < _bots.Count && _beaten.Contains(_bots[index - 1].Id));

    /// <summary>The highest unlocked rung the player has not yet beaten - where to play next.</summary>
    public int CurrentIndex
    {
        get
        {
            for (int i = 0; i < _bots.Count; i++)
                if (IsUnlocked(i) && !_beaten.Contains(_bots[i].Id))
                    return i;
            return _bots.Count - 1; // ladder cleared
        }
    }

    public IReadOnlyList<LadderRung> Rungs() =>
        _bots.Select((b, i) => new LadderRung(i, b, HintsFor(i), IsUnlocked(i), _beaten.Contains(b.Id))).ToList();

    /// <summary>
    /// Record a game result against a rung. A win on an unlocked rung marks it beaten (and unlocks the
    /// next); returns true when that is a newly recorded win. Wins on locked rungs are ignored.
    /// </summary>
    public bool RecordResult(int index, bool playerWon)
    {
        if (!playerWon || index < 0 || index >= _bots.Count || !IsUnlocked(index))
            return false;
        return _beaten.Add(_bots[index].Id);
    }
}
