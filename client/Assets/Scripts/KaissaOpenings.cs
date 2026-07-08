using System.IO;
using Kaissa.Training;
using UnityEngine;

// Local persistence for the opening-repertoire spaced-repetition schedule, kept in the same folder
// as the other player data.
public static class KaissaOpenings
{
    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "kaissa-openings.json");

    public static OpeningProgress Load() =>
        File.Exists(Path) ? OpeningProgress.FromJson(File.ReadAllText(Path)) : new OpeningProgress();

    public static void Save(OpeningProgress progress) => File.WriteAllText(Path, progress.ToJson());
}
