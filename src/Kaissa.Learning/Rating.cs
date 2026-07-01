namespace Kaissa.Learning;

/// <summary>
/// The four FSRS grades for a single review. In Kaissa these are not chosen by the player;
/// they are derived from in-game performance (see docs/learning-engine.md), but the scheduler
/// math is identical either way. The integer values match the FSRS convention (1..4).
/// </summary>
public enum Rating
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4,
}
