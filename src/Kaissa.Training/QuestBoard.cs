using System;
using System.Collections.Generic;
using System.Linq;

namespace Kaissa.Training;

/// <summary>A snapshot of the player's progress, taken from the local stores, that the quest board reads.</summary>
public sealed record QuestSnapshot(
    int PuzzlesSolved, int GamesWon, int BestPuzzleStreak, int DayStreak,
    int BotsBeaten, int MemoryBest, int VisualizationBest, int SoloBest);

/// <summary>A graduated goal with a current value toward a target (unlike a boolean achievement).</summary>
public sealed record Quest(string Title, string Description, int Current, int Target)
{
    public bool Done => Current >= Target;
    public double Fraction => Target <= 0 ? 1 : Math.Clamp((double)Current / Target, 0, 1);
}

/// <summary>
/// A graduated curriculum: a fixed set of quests scored from the player's own progress, plus a named rank
/// ladder (Pawn through King) driven by how many quests are complete. Pure and deterministic; a spine for
/// spaced play, cosmetic only - it never gates training.
/// </summary>
public static class QuestBoard
{
    // Rank names in ascending order; the rank is chosen by the count of completed quests.
    private static readonly string[] Ranks = { "Pawn", "Knight", "Bishop", "Rook", "Queen", "King" };

    public static IReadOnlyList<Quest> For(QuestSnapshot s) => new[]
    {
        new Quest("Solver", "Solve 50 puzzles", s.PuzzlesSolved, 50),
        new Quest("Streaker", "Reach a 15-puzzle streak", s.BestPuzzleStreak, 15),
        new Quest("Competitor", "Win 10 games", s.GamesWon, 10),
        new Quest("Ladder", "Beat 5 bots on the ladder", s.BotsBeaten, 5),
        new Quest("Regular", "Keep a 7-day streak", s.DayStreak, 7),
        new Quest("Memory", "Reach Memory level 10", s.MemoryBest, 10),
        new Quest("Blindfold", "Reach Visualization level 4", s.VisualizationBest, 4),
        new Quest("Solo", "Clear Solo Chess level 8", s.SoloBest, 8),
    };

    public static int CompletedCount(QuestSnapshot s) => For(s).Count(q => q.Done);

    /// <summary>The player's rank and its index, chosen by completed-quest count (one rank per quest, up
    /// to the top rank once enough are done).</summary>
    public static (string Name, int Index) Rank(QuestSnapshot s)
    {
        int done = CompletedCount(s);
        int index = Math.Clamp(done, 0, Ranks.Length - 1);
        return (Ranks[index], index);
    }

    public static int RankCount => Ranks.Length;
}
