using System.IO;
using UnityEngine;

// Loads/saves the player's training progress (the serialized SkillModel) to a local file, shared
// across all modes so stats and mastery persist between sessions.
public static class KaissaProgress
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "kaissa-progress.json");

    public static string? Load() => File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;

    public static void Save(string json) => File.WriteAllText(FilePath, json);
}
