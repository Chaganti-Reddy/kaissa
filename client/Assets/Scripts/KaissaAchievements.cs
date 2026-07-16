using System;
using System.Collections.Generic;
using System.Linq;
using Kaissa.Training;
using Kaissa.Training.Api;

// Milestone badges, computed from the player's own saved progress. Cosmetic only - achievements never
// gate training or buy strength; they just mark the road travelled. Evaluated on demand from the local
// stores (game log, per-mode bests, the bot ladder, the day streak, puzzle stats).
public static class KaissaAchievements
{
    public sealed class Achievement
    {
        public string Name { get; }
        public string Description { get; }
        public bool Earned { get; }
        public Achievement(string name, string description, bool earned)
        {
            Name = name; Description = description; Earned = earned;
        }
    }

    public static IReadOnlyList<Achievement> All(PlayerStats stats)
    {
        var beaten = KaissaSettings.BotsBeaten
            .Split(',', StringSplitOptions.RemoveEmptyEntries);
        int beatenCount = beaten.Length;
        bool beatStrong = beaten.Any(id => (BotRoster.ById(id)?.Elo ?? 0) >= 2000);
        int puzzleStreak = Math.Max(stats.BestStreak, KaissaSettings.PuzzleBestStreak);
        int rushBest = Math.Max(KaissaSettings.RushBest3, Math.Max(KaissaSettings.RushBest5, KaissaSettings.RushBestSurvival));

        return new[]
        {
            new Achievement("First blood", "Win a game against a bot", KaissaGameLog.Wins >= 1),
            new Achievement("Regular", "Play 25 games", KaissaGameLog.Count >= 25),
            new Achievement("Ladder climber", "Beat 3 bots on the ladder", beatenCount >= 3),
            new Achievement("Giant slayer", "Beat a bot rated 2000 or above", beatStrong),
            new Achievement("Century", "Solve 100 puzzles", stats.TotalCorrect >= 100),
            new Achievement("On a roll", "Reach a 10-puzzle streak", puzzleStreak >= 10),
            new Achievement("Storm chaser", "Score 20 in Puzzle Blitz", rushBest >= 20),
            new Achievement("Photographic", "Reach Memory level 8", KaissaSettings.MemoryBest >= 8),
            new Achievement("Eagle eye", "Score 15 in Captures & Threats", KaissaSettings.CapturesBest >= 15),
            new Achievement("Blindfolded", "Reach Visualization level 3", KaissaSettings.VisualizationBest >= 3),
            new Achievement("Solo artist", "Clear Solo Chess level 8", KaissaSettings.SoloBest >= 8),
            new Achievement("Dedicated", "Keep a 7-day streak", KaissaStreak.CurrentDays() >= 7),
        };
    }

    public static (int earned, int total) Progress(PlayerStats stats)
    {
        var all = All(stats);
        return (all.Count(a => a.Earned), all.Count);
    }
}
